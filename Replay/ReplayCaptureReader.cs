// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Timeline;

namespace ReplayCapture;

public class ReplayCaptureReader
{
    public struct FrameRange
    {
        public int Start;
        public int End;

        public bool InRange(int t) => t >= Start && t <= End;
        public bool Overlaps(FrameRange other) => (other.Start < this.Start) ? (other.End >= this.Start) : (other.Start <= this.End);
    }

    public class FrameStampedList<T> : IReadOnlyList<(int frame, T val)>
    {
        private List<(int frame, T val)> baking_list = new();

        private int[] internal_frames = new int[0];
        private T[] internal_values = new T[0];

        public void AddForBake(int frame, T entry)
        {
            // TODO: Assert that times are monotonic
            baking_list.Add((frame, entry));
        }

        public void Bake()
        {
            internal_frames = baking_list.Select(x => x.frame).ToArray();
            internal_values = baking_list.Select(x => x.val).ToArray();
            baking_list.Clear();
            baking_list = null;
        }

        public void Load(IEnumerable<(int frame,T val)> values)
        {
            // TODO: Assert that times are monotonic
            internal_frames = values.Select(x => x.frame).ToArray();
            internal_values = values.Select(x => x.val).ToArray();
            baking_list.Clear();
            baking_list = null;
        }

        public int FirstIndexFor(int frame)
        {
            if (Count == 0) return -1;
            // Note: Could be optimized with a binary search.
            int index = 0;
            for (; index < Count; ++index)
            {
                if (internal_frames[index] >= frame) break;
            }
            return index >= Count ? (Count - 1) : index;
        }

        public T FirstAtFrame(int frame)
        {
            int index = FirstIndexFor(frame);
            return (index < 0) ? default(T) : internal_values[index];
        }

        public IEnumerable<(int frame, T val)> SubRange(FrameRange range)
        {
            for (int index = FirstIndexFor(range.Start); index < internal_frames.Length; ++index)
            {
                int frame = internal_frames[index];
                if (range.InRange(frame))
                {
                    yield return (frame, internal_values[index]);
                }
                if (frame > range.End) break;
            }
        }

        public IEnumerable<(int frame, T val)> ForFrame(int targetFrame)
        {
            int index = FirstIndexFor(targetFrame);
            for (; index >= 0 && index < Count; ++index)
            {
                int frame = internal_frames[index];
                if (frame == targetFrame)
                {
                    yield return (frame, internal_values[index]);
                }
                if (frame > targetFrame) break;
            }
        }

        public IEnumerable<T> AtFrame(int targetFrame) => ForFrame(targetFrame).Select(x=>x.val);

        public IEnumerator<(int frame, T val)> GetEnumerator()
        {
            for (int index = 0; index < Count; ++index)
            {
                yield return this[index];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            for (int index = 0; index < Count; ++index)
            {
                yield return this[index];
            }
        }

        public IEnumerable<T> AllValues => internal_values;

        public int Count => internal_values.Length;

        public (int frame, T val) this[int index] => (internal_frames[index],internal_values[index]);
    }

    public class ForDict<K, V> : Dictionary<K, V> where V : class, new()
    {
        public V For(K key)
        {
            if (key == null) return null;
            if (!TryGetValue(key, out V val))
            {
                val = new V();
                Add(key, val);
            }
            return val;
        }
    }

    public class FrameStampedListDict<T,V> : ForDict<T, FrameStampedList<V>>
    {
        public void Bake()
        {
            foreach(var fsl in Values)
            {
                fsl.Bake();
            }
        }
    }

    public int GetFrameForTime(double t)
    {
        if (t <= 0 || Double.IsNaN(t)) return 0;
        int t_indx = (int)Math.Abs(t);
        if (t_indx >= FramesForTimes.Length) { t_indx = FramesForTimes.Length - 1; }
        int index = FramesForTimes[t_indx];
        for(; index < FrameTimes.Length; ++index)
        {
            if (FrameTimes[index]>=t)
            {
                return (index > 0) ? (index - 1) : 0;
            }
        }
        return FrameTimes.Length - 1;
    }

    public double GetTimeForFrame(int frame)
    {
        int frameCount = FrameTimes.Length;
        if (frameCount == 0) return 0;
        if (frame <= 0) return 0;
        if (frame >= frameCount) return FrameTimes[frameCount - 1];
        return FrameTimes[frame];
    }

    public FrameRange GetFramesForTimes(double start, double end) => new() { Start = GetFrameForTime(start), End = GetFrameForTime(end) };

    public double GetFrameTimes(double t, out double previousFrameTime, out double nextFrameTime)
    {
        int currentFrame = GetFrameForTime(t);
        double currentFrameTime = GetTimeForFrame(currentFrame);
        nextFrameTime = GetTimeForFrame(currentFrame + 1);
        previousFrameTime = GetTimeForFrame(currentFrame - 1);
        return currentFrameTime;
    }

    public IEnumerable<(int, double, double)> GetWindowFrameRatios(ITimelineWindow window)
    {
        double lastRatio = 0;
        double windowStart = window.Start;
        double windowEnd = window.End;
        double windowLength = window.End - window.Start;
        int frameNum = 0;
        foreach (float t in FrameTimes)
        {
            if (t > windowEnd)
            {
                yield return (frameNum, lastRatio, 1);
                break;
            }
            else if (t > windowStart)
            {
                double ratio = (t - windowStart) / windowLength;
                yield return (frameNum, lastRatio, ratio);
                lastRatio = ratio;
            }
            ++frameNum;
        }
    }

    public double TotalTime => FrameTimes[FrameTimes.Length - 1];

    public FrameStampedList<Transform> GetEntityTransforms(Entity entity)
    {
        return EntitySetTransforms.TryGetValue(entity, out var transforms) ? transforms : null;
    }

    public Transform GetEntityTransform(Entity entity, int frame)
    {
        return EntitySetTransforms.TryGetValue(entity, out var transforms) ? transforms.FirstAtFrame(frame) : entity.InitialTransform;
    }
    public Transform GetEntityTransform(Entity entity, double time) => GetEntityTransform(entity, GetFrameForTime(time));
    public Point GetEntityPosition(Entity entity, int frame) => GetEntityTransform(entity, frame).Translation;
    public Point GetEntityPosition(Entity entity, double time) => GetEntityPosition(entity, GetFrameForTime(time));

    public IEnumerable<Entity> EntitiesWithTransforms => EntitySetTransforms.Keys;

    public FrameRange GetEntityLifeTime(Entity entity)
    {
        if (EntityLifeTimes.TryGetValue(entity, out var frameRange))
        {
            return frameRange;
        }
        return new FrameRange() { Start = entity.CreationFrame, End = FrameTimes.Length - 1 };
    }

    public IDictionary<string, string> GetDynamicParamsAt(Entity entity, int frame)
    {
        // TODO: pre-index this
        var dict = new Dictionary<string, string>();
        var paramsStream = EntityDynamicParamsCombined.For(entity);
        foreach (var paramsEntry in paramsStream)
        {
            if (paramsEntry.frame > frame) break;
            dict[paramsEntry.val.name] = paramsEntry.val.val;
        }
        return dict;
    }

    static readonly char[] SplitSeparators = new char[] { '.', '\\', '/' };

    public struct DynamicParamTimeEntry
    {
        public ReplayCaptureReader replay;
        public string name;
        public string val;
        public int frame;
        public int depth;

        public IEnumerable<string> SplitValues => string.IsNullOrEmpty(val) ? Enumerable.Empty<string>() : val.Split(SplitSeparators);

        public double time => replay.GetTimeForFrame(frame);
    }

    private void AddToDynamicPropertiesTable(Entity entity, int frame, string param, string val)
    {
        EntityDynamicParamsCombined.For(entity)?.AddForBake(frame, (param, val));

        EntityDynamicParamsNames.Add(param);

        if (!EntityDynamicParams.TryGetValue(entity, out var tbl))
        {
            tbl = new();
            EntityDynamicParams[entity] = tbl;
        }
        if (!tbl.TryGetValue(param, out var lst))
        {
            lst = new();
            tbl[param] = lst;
        }
        if ( lst.Count == 0 || lst.Last().val != val ) // Filter out redundant param sets
        {
            lst.Add(new DynamicParamTimeEntry() { replay = this, name = param, frame = frame, val = val, depth = string.IsNullOrEmpty(val) ? 0 : (val.Split(SplitSeparators).Length - 1) });
        }
    }

    public IEnumerable<(string name, float val)> GetDynamicValuesAt(Entity entity, int frame)
    {
        // TODO: pre-index this
        var dict = new Dictionary<string, float>();
        var paramsStream = EntityDynamicValues.For(entity);
        foreach (var paramsEntry in paramsStream)
        {
            if (paramsEntry.frame > frame) break;
            dict[paramsEntry.val.name] = paramsEntry.val.val;
        }
        return dict.Select(x=>(x.Key,x.Value));
    }

    public IEnumerable<(string name,string val)> AllParametersAt(Entity entity, int frame)
    {
        if (entity != null)
        {
            yield return ("Name", entity.Name);
            yield return ("Path", entity.Path);
            yield return ("Id", $"{entity.Id}({entity.ParentId})");
            bool isActive = GetEntityLifeTime(entity).InRange(frame);
            //yield return ("Active", $"{isActive}");
            if (!isActive) { yield break; }
            var pos = GetEntityPosition(entity, frame);
            yield return ("Position", $"({pos.X},{pos.Y},{pos.Z})");
            foreach (var sp in entity.StaticParameters) yield return (sp.Key,sp.Value);
            foreach (var dp in GetDynamicParamsAt(entity, frame)) yield return (dp.Key,dp.Value);
            foreach ((string name, float val) in GetDynamicValuesAt(entity,frame)) yield return (name, val.ToString());
        }

    }

    public HashSet<string> EntityCategories { get; private set; } = new();
    public HashSet<string> LogCategories { get; private set; } = new();
    public IEnumerable<string> ParameterCategories => EntityDynamicParamsNames;
    public HashSet<Color> LogColors { get; private set; } = new();

    public HashSet<string> DrawCategories { get; private set; } = new();
    public IEnumerable<string> GetDrawCategories() => DrawCommands.Where(x=>!x.val.IsCreationDraw).Select(x => x.val.category).Distinct();
    public HashSet<Color> DrawColors { get; private set; } = new();

    public float[] FrameTimes { get; private set; } = new float[1];
    public int[] FramesForTimes { get; private set; } = new int[1]; // Skip list
    //public List<EntityEx> Entities { get; private set; } = new();
    public Dictionary<int,EntityEx> Entities { get; private set; } = new();
    public EntityGraphNode EntitiesGraph { get; private set; } = new();

    public Dictionary<Entity, FrameRange> EntityLifeTimes { get; private set; } = new();
    private FrameStampedListDict<Entity,Transform> EntitySetTransforms { get; set; } = new();
    public FrameStampedList<(Entity entity, string category, string message, Color color)> LogEntries { get; private set; } = new();
    public FrameStampedListDict<Entity, (string name, string val)> EntityDynamicParamsCombined { get; private set; } = new();
    public HashSet<string> EntityDynamicParamsNames { get; private set; } = new();
    public Dictionary<Entity, Dictionary<string, List<DynamicParamTimeEntry>>> EntityDynamicParams { get; private set; } = new();
    public FrameStampedListDict<Entity, (string name, float val)> EntityDynamicValues { get; private set; } = new();

    public Dictionary<Entity, List<int>> LogEntityFrameMarkers = new();

    public enum EntityDrawCommandType { None, Line, Circle, Sphere, Box, Capsule, Mesh };
    public record EntityDrawCommand
    {
        public EntityEx entity;
        public string category;
        public EntityDrawCommandType type;
        public Color color;
        public Transform xform;
        public Point p2; // temp, should be implicit from transform and scale.
        public Point[] verts;
        public double scale;
        public int frame;

        public Point Pos => xform.Translation;
        public Point EndPoint => p2;
        public Point UpVect => p2;
        public Point Dimensions => p2;
        public double Radius => scale;

        public bool IsCreationDraw => entity != null && ((entity as EntityEx).RegistrationFrame == frame || (entity as EntityEx).CreationFrame == frame) && string.IsNullOrEmpty(category);

        //public System.Windows.Media.Colors WindowsColor => Enum.Parse(typeof(System.Windows.Media.Colors), color.ToString());
    }
    public FrameStampedList<EntityDrawCommand> DrawCommands { get; private set; } = new();

    public ReplayCaptureReader(string filePath)
    {
        LoadCapture(filePath);
    }

    private void LoadCapture(string filePath)
    {
        if (File.Exists(filePath))
        {
            Stream stream = File.OpenRead(filePath);
            
            // Auto-detect compression
            int header = (new BinaryReader(stream)).ReadInt32();
            if (header == (int)BlockType.ReplayHeader)
            {
                // uncompressed
            }
            else
            {
                stream.Seek(0, SeekOrigin.Begin);

                // Wrap in a deflate stream
                stream = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress);
                stream = new BufferedStream(stream, 64<<10); // Massively improves read performance
            }

            try
            {
                LoadCapture(new BinaryReaderEx(stream, BinaryReplayWriter.StringEncoding));
            }
            catch (Exception e)
            {
                // The compressed streams sometimes fail not as EndOfStreamException.
                System.Diagnostics.Debug.WriteLine($"{e}");
            }
        }
    }

    private void LoadCapture(BinaryReaderEx reader)
    {
        var frametimes = new List<float>() { 0 };
        var framesForTimes = new List<int>() { 0 };
        try
        {


            int blockCount = 0;
            Dictionary<Entity, Transform> last_xforms = new();
            while(true)
            {
                ++blockCount;
                //System.Diagnostics.Debug.WriteLine($"Processing block #{blockCount}");
                int blockTypeVal = reader.Read7BitEncodedInt();
                if (blockTypeVal == 0 || !Enum.IsDefined(typeof(BlockType), blockTypeVal)) throw new InvalidOperationException("Invalid block type. Probably not a valid replay file.");

                BlockType blockType = (BlockType)blockTypeVal;

                if (blockType == BlockType.ReplayHeader)
                {
                    // empty
                }
                else if (blockType == BlockType.FrameStep)
                {
                    float totalTime = reader.ReadSingle();
                    frametimes.Add(totalTime);
                    if (Math.Abs(totalTime) > framesForTimes.Count) { framesForTimes.Add(frametimes.Count); }
                }
                else
                {
                    int frame = reader.Read7BitEncodedInt();
                    int id = reader.Read7BitEncodedInt();
                    EntityEx? entity = null;

                    int parentId = -1;
                    if (blockType == BlockType.EntityDefWithParent)
                    {
                        parentId = reader.Read7BitEncodedInt();
                    }

                    if (blockType == BlockType.EntityDef || blockType == BlockType.EntityDefWithParent)
                    {
                        reader.Read(out EntityEx entitydef);
                        entitydef.ParentId = parentId;
                        entity = entitydef;
                        if (Entities.TryGetValue(id, out var previouslyDefinedEntity))
                        {
                            // Overrides
                            previouslyDefinedEntity.Name = entitydef.Name;
                            previouslyDefinedEntity.Path = entitydef.Path;
                            previouslyDefinedEntity.CategoryName = entitydef.CategoryName;
                            previouslyDefinedEntity.TypeName = entitydef.TypeName;
                            previouslyDefinedEntity.InitialTransform = entitydef.InitialTransform;
                            previouslyDefinedEntity.StaticParameters = entitydef.StaticParameters;
                            previouslyDefinedEntity.RegistrationFrame = entitydef.CreationFrame;
                            entity = previouslyDefinedEntity;
                        }
                        else
                        {
                            Entities.Add(id, entitydef);
                        }
                        EntityCategories.Add(entitydef.CategoryName);
                    }

                    if (entity == null)
                    {
                        if (!Entities.TryGetValue(id, out entity))
                        {
                            // Placeholder entity
                            entity = new();
                            entity.Id = id;
                            Entities.Add(id, entity);
                        }
                    }

                    switch (blockType)
                    {
                        case BlockType.EntityUndef:
                            {
                                if (entity != null) EntityLifeTimes[entity] = new FrameRange() { Start = entity.CreationFrame, End = frame };
                            }
                            break;
                        case BlockType.EntitySetPos:
                            {
                                reader.Read(out Point p);
                                if (!last_xforms.TryGetValue(entity, out Transform xform)) { xform = new Transform(); xform.Rotation.W = 1; }
                                xform.Translation = p;
                                EntitySetTransforms.For(entity)?.AddForBake(frame, xform);
                                entity.HasTransforms = true;
                                last_xforms[entity] = xform;
                            }
                            break;
                        case BlockType.EntitySetTransform:
                            {
                                reader.Read(out Transform xform);
                                EntitySetTransforms.For(entity)?.AddForBake(frame, xform);
                                entity.HasTransforms = true;
                                last_xforms[entity] = xform;
                            }
                            break;
                        case BlockType.EntityLog:
                            {
                                string category = reader.ReadString();
                                string msg = reader.ReadString();
                                msg = msg.Replace('\r',' '); // newlines stripped
                                msg = msg.Replace('\n',' '); // newlines stripped
                                reader.Read(out Color color);
                                List<int>? framesWithLogs = null;
                                if (entity != null)
                                {
                                    entity.HasLogs = true;
                                    entity.HasLogsPastFirstFrame |= frame > entity.CreationFrame;
                                    LogEntries.AddForBake(frame, (entity, category, msg, color));

                                    // Add per entity frame markers
                                    if (!LogEntityFrameMarkers.TryGetValue(entity, out framesWithLogs))
                                    {
                                        framesWithLogs = new();
                                        LogEntityFrameMarkers.Add(entity, framesWithLogs);
                                    }
                                }
                                else
                                {
                                    framesWithLogs = new();
                                }
                                if (framesWithLogs.Count == 0 || framesWithLogs.Last() != frame) { framesWithLogs.Add(frame); }

                                LogCategories.Add(category);
                                LogColors.Add(color);
                            }
                            break;
                        case BlockType.EntityParameter:
                            {
                                string label = reader.ReadString();
                                string val = reader.ReadString();
                                entity.HasParameters = true;
                                AddToDynamicPropertiesTable(entity, frame, label, val);
                            }
                            break;
                        case BlockType.EntityValue:
                            {
                                string label = reader.ReadString();
                                float val = reader.ReadSingle();
                                entity.HasNumericParameters = true;
                                EntityDynamicValues.For(entity)?.AddForBake(frame, (label, val));
                            }
                            break;
                        case BlockType.EntityLine:
                            {
                                string category = reader.ReadString();
                                reader.Read(out Point p1);
                                reader.Read(out Point p2);
                                reader.Read(out Color color);
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Line, color = color, frame = frame, xform = new Transform() { Translation = p1 }, p2 = p2, scale = 1 });
                            }
                            break;
                        case BlockType.EntityCircle:
                            {
                                string category = reader.ReadString();
                                reader.Read(out Point center);
                                reader.Read(out Point up);
                                float radius = reader.ReadSingle();
                                reader.Read(out Color color);
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Circle, color = color, frame = frame, xform = new Transform() { Translation = center }, p2 = up, scale = radius });
                            }
                            break;
                        case BlockType.EntitySphere:
                            {
                                string category = reader.ReadString();
                                reader.Read(out Point center);
                                float radius = reader.ReadSingle();
                                reader.Read(out Color color);
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Sphere, color = color, frame = frame, xform = new Transform() { Translation = center }, scale = radius });
                            }
                            break;
                        case BlockType.EntityBox:
                            {
                                string category = reader.ReadString();
                                reader.Read(out Transform xform);
                                reader.Read(out Point dimensions);
                                reader.Read(out Color color);
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Box, color = color, frame = frame, xform = xform, p2 = dimensions, scale = 1 });
                            }
                            break;
                        case BlockType.EntityCapsule:
                            {
                                string category = reader.ReadString();
                                reader.Read(out Point p1);
                                reader.Read(out Point p2);
                                float radius = reader.ReadSingle();
                                reader.Read(out Color color);
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Capsule, color = color, frame = frame, xform = new Transform() { Translation = p1 }, p2 = p2, scale = radius });
                            }
                            break;
                        case BlockType.EntityMesh:
                            {
                                string category = reader.ReadString();
                                int vertexCount = reader.ReadInt32();
                                Point[] verts = new Point[vertexCount];
                                for(int i = 0; i < vertexCount; ++i) { reader.Read(out Point p); verts[i] = p; }
                                reader.Read(out Color color);
                                entity.HasMesh = true;
                                AddDrawCommand(frame, new EntityDrawCommand() { entity = entity, category = category, type = EntityDrawCommandType.Mesh, verts = verts, color = color, frame = frame });
                            }
                            break;
                    }
                }
            }
        }
        catch (EndOfStreamException)
        {
            // EOF
        }
        this.FrameTimes = frametimes.ToArray();
        this.FramesForTimes = framesForTimes.ToArray();
        LogEntries.Bake();
        DrawCommands.Bake();
        EntitySetTransforms.Bake();
        EntityDynamicParamsCombined.Bake();
        EntityDynamicValues.Bake();

        EntitiesGraph = EntityGraphNode.BuildGraph(Entities.Values);

        foreach (var entity in Entities.Values)
        {
            entity.CreationDrawsCommands = DrawCommands.AtFrame(entity.CreationFrame).Where(x => x.entity == entity && x.IsCreationDraw)
                        .Concat(DrawCommands.AtFrame(entity.RegistrationFrame).Where(x => x.entity == entity && x.IsCreationDraw))
                        .ToList();
        }
    }

    private void AddDrawCommand(int frame, EntityDrawCommand dc)
    {
        DrawColors.Add(dc.color);
        if (!dc.IsCreationDraw) DrawCategories.Add(dc.category);
        DrawCommands.AddForBake(frame, dc);
        dc.entity.HasDraws = true;
    }
}

public class EntityEx : Entity
{
    public int RegistrationFrame;
    public bool HasTransforms;
    public bool HasLogs;
    public bool HasLogsPastFirstFrame;
    public bool HasDraws;
    public bool HasMesh;
    public bool HasParameters;
    public bool HasNumericParameters;

    public List<ReplayCaptureReader.EntityDrawCommand> CreationDrawsCommands = new();
}

public class EntityGraphNode
{
    public EntityEx? Entity;
    public List<EntityGraphNode> Children = new();
    public EntityGraphNode? Parent;

    public string Name => Entity?.Name ?? "root";
    public string PathName => (Parent?.Entity != null) ? $"{Parent.PathName}.{Name}" : Name;

    public EntityGraphNode? FindNode(int id)
    {
        if (Entity != null && Entity.Id == id) return this;
        foreach (var childnode in Children.Select(x => x.FindNode(id)))
        {
            if (childnode != null) return childnode;
        }
        return null;
    }

    public IEnumerable<EntityGraphNode> FindNodeWithParent(int parent_id)
    {
        return EnumerateDepthFirst().ToList().Where(x => x.Entity != null && x.Entity.HasParent && x.Entity.ParentId == parent_id);
    }

    public void AddEntity(EntityEx e)
    {
        EntityGraphNode node = new() { Entity = e };

        if ((!e.HasParent)
            || (Entity != null && Entity.Id == e.ParentId))
        {
            node.Parent = this;
            Children.Add(node);
        }
        else
        {
            var parentNode = this.FindNode(e.ParentId);
            if (parentNode != null)
            {
                parentNode.AddEntity(e);
            }
            else
            {
                // Parent not yet in the graph, add it here for the time being
                node.Parent = this;
                Children.Add(node);
            }      
        }

        // Nodes can be added before their parents, so we need to reparent
        foreach (var childNode in this.FindNodeWithParent(e.Id))
        {
            childNode.Parent.Children.Remove(childNode);
            childNode.Parent = node;
            childNode.Parent.Children.Add(childNode);
        }

    }

    public static EntityGraphNode BuildGraph(IEnumerable<EntityEx> entities)
    {
        EntityGraphNode root = new();
        foreach (var e in entities)
        {
            root.AddEntity(e);
        }
        return root;
    }

    public IEnumerable<EntityGraphNode> EnumerateDepthFirst()
    {
        yield return this;
        foreach (EntityGraphNode childnode in Children)
        {
            foreach (var child in childnode.EnumerateDepthFirst())
            {
                yield return child; 
            }
        }
    }

    public IEnumerable<EntityEx> EnumerateEntitiesDepthFirst()
    {
        if (Entity != null)
        {
            yield return Entity;
        }
        foreach (var childnode in Children)
        {
            foreach (var childEntity in childnode.EnumerateEntitiesDepthFirst())
            {
                yield return childEntity;
            }
        }
    }
}

internal static class BinaryIOExtensionsRead
{
    public static void Read(this BinaryReaderEx r, out EntityEx entity)
    {
        entity = new EntityEx();
        entity.Id = r.Read7BitEncodedInt();
        entity.Name = r.ReadString();
        entity.Path = r.ReadString();
        entity.TypeName = r.ReadString();
        entity.CategoryName = r.ReadString();
        r.Read(out entity.InitialTransform);
        r.Read(out entity.StaticParameters);
        entity.CreationFrame = r.Read7BitEncodedInt();
    }

    public static void Read(this BinaryReader r, out Point point)
    {
        point = new Point()
        {
            X = r.ReadSingle(),
            Y = r.ReadSingle(),
            Z = r.ReadSingle()
        };
    }

    public static void Read(this BinaryReader r, out Quaternion quat)
    {
        quat = new Quaternion()
        {
            X = r.ReadSingle(),
            Y = r.ReadSingle(),
            Z = r.ReadSingle(),
            W = r.ReadSingle()
        };
    }

    public static void Read(this BinaryReader r, out Transform xform)
    {
        r.Read(out Point t);
        r.Read(out Quaternion rot);
        xform = new Transform() { Translation = t, Rotation = rot };
    }

    public static void Read(this BinaryReaderEx r, out Dictionary<string, string> stringDict)
    {
        stringDict = new Dictionary<string, string>();
        int count = r.Read7BitEncodedInt();
        while (count-- > 0)
        {
            //stringDict.Add(r.ReadString(), r.ReadString());
            stringDict[r.ReadString()] = r.ReadString();
        }
    }

    public static void Read(this BinaryReaderEx r, out Color color)
    {
        color = (Color)r.Read7BitEncodedInt();
    }
}

// 7BitEncodedInt marked protected in prior versions of .net
public class BinaryReaderEx : BinaryReader
{
    public BinaryReaderEx(Stream input, System.Text.Encoding encoding) : base(input, encoding) { }
    public new int Read7BitEncodedInt() => base.Read7BitEncodedInt();
}
