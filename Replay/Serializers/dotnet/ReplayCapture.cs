// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;
using System.IO;

namespace ReplayCapture
{
    public struct Point { public float X, Y, Z; }
    public struct Quaternion
    {
        public float X, Y, Z, W;
        public static readonly Quaternion Identity = new Quaternion() { W = 1 };
    }

    public struct Transform
    {
        public Point Translation;
        public Quaternion Rotation;

        public static readonly Transform Identity = new Transform() { Rotation = Quaternion.Identity };
    }

    // Same as System.Windows.Media.Colors, but without the dependency
    public enum Color { AliceBlue, PaleGoldenrod, Orchid, OrangeRed, Orange, OliveDrab, Olive, OldLace, Navy, NavajoWhite, Moccasin, MistyRose, MintCream, MidnightBlue, MediumVioletRed, MediumTurquoise, MediumSpringGreen, MediumSlateBlue, LightSkyBlue, LightSlateGray, LightSteelBlue, LightYellow, Lime, LimeGreen, PaleGreen, Linen, Maroon, MediumAquamarine, MediumBlue, MediumOrchid, MediumPurple, MediumSeaGreen, Magenta, PaleTurquoise, PaleVioletRed, PapayaWhip, SlateGray, Snow, SpringGreen, SteelBlue, Tan, Teal, SlateBlue, Thistle, Transparent, Turquoise, Violet, Wheat, White, WhiteSmoke, Tomato, LightSeaGreen, SkyBlue, Sienna, PeachPuff, Peru, Pink, Plum, PowderBlue, Purple, Silver, Red, RoyalBlue, SaddleBrown, Salmon, SandyBrown, SeaGreen, SeaShell, RosyBrown, Yellow, LightSalmon, LightGreen, DarkRed, DarkOrchid, DarkOrange, DarkOliveGreen, DarkMagenta, DarkKhaki, DarkGreen, DarkGray, DarkGoldenrod, DarkCyan, DarkBlue, Cyan, Crimson, Cornsilk, CornflowerBlue, Coral, Chocolate, AntiqueWhite, Aqua, Aquamarine, Azure, Beige, Bisque, DarkSalmon, Black, Blue, BlueViolet, Brown, BurlyWood, CadetBlue, Chartreuse, BlanchedAlmond, DarkSeaGreen, DarkSlateBlue, DarkSlateGray, HotPink, IndianRed, Indigo, Ivory, Khaki, Lavender, Honeydew, LavenderBlush, LemonChiffon, LightBlue, LightCoral, LightCyan, LightGoldenrodYellow, LightGray, LawnGreen, LightPink, GreenYellow, Gray, DarkTurquoise, DarkViolet, DeepPink, DeepSkyBlue, DimGray, DodgerBlue, Green, Firebrick, ForestGreen, Fuchsia, Gainsboro, GhostWhite, Gold, Goldenrod, FloralWhite, YellowGreen }

    public interface IReplayWriter : IDisposable
    {
        void RegisterEntity(object obj, string name, string path, string typename, string categoryname, Transform initialTransofrm, Dictionary<string, string> staticParameters = null);
        void UnRegisterEntity(object obj);
        void SetPosition(object obj, Point pos);
        void SetTransform(object obj, Transform xform);
        void SetLog(object obj, string category, string log, Color color);
        void SetDynamicParam(object obj, string key, string val);
        void SetDynamicParam(object obj, string key, float val);
        void DrawSphere(object obj, string category, Point pos, float radius, Color color);
        void DrawBox(object obj, string category, Transform xform, Point dimensions, Color color);
        void DrawCapsule(object obj, string category, Point p1, Point p2, float radius, Color color);
        void DrawMesh(object obj, string category, Point[] verts, Color color);
        void DrawLine(object obj, string category, Point p1, Point p2, Color color);
        void DrawCircle(object obj, string category, Point position, Point up, float radius, Color color);

        void StepFrame(float totalTime);
    }

    public class ReplayCaptureWriter : IReplayWriter, IDisposable
    {
        public ReplayCaptureWriter(string filePath)
        {
            var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            Writer = new BinaryReplayWriter(stream);
        }

        public ReplayCaptureWriter(Stream stream)
        {
            Writer = new BinaryReplayWriter(stream);
        }

        public void Dispose()
        {
            Writer?.Dispose();
            Writer = null;
        }

        public void StepFrame(float totalTime)
        {
            Writer?.WriteFrameStep(FrameCounter, totalTime);
            ++FrameCounter;
        }

        public void RegisterEntity(object obj, string name, string path, string typename, string categoryname, Transform initialTransofrm, Dictionary<string, string> staticParameters = null)
        {
            EntityCounter++;
            var entity = new Entity()
            {
                Id = EntityCounter,
                Name = name,
                Path = path,
                TypeName = typename,
                CategoryName = categoryname,
                InitialTransform = initialTransofrm,
                StaticParameters = staticParameters ?? new Dictionary<string, string>(),
                CreationFrame = FrameCounter
            };
            Entities.Add(entity);
            EntityMapping[obj] = entity;

            Writer?.WriteEntityDef(entity, FrameCounter);
        }

        public void UnRegisterEntity(object obj)
        {
            Writer?.WriteEntityUndef(GetEntity(obj), FrameCounter);
            EntityMapping.Remove(obj);
        }

        public void SetPosition(object obj, Point pos) => SetPosition(GetEntity(obj), pos);
        public void SetTransform(object obj, Transform xform) => SetTransform(GetEntity(obj), xform);
        public void SetLog(object obj, string category, string log, Color color) => SetLog(GetEntity(obj), category, log, color);
        public void SetDynamicParam(object obj, string key, string val) => SetDynamicParam(GetEntity(obj), key, val);
        public void SetDynamicParam(object obj, string key, float val) => SetDynamicParam(GetEntity(obj), key, val);
        public void DrawSphere(object obj, string category, Point pos, float radius, Color color) => DrawSphere(GetEntity(obj), category, pos, radius, color);
        public void DrawBox(object obj, string category, Transform xform, Point dimensions, Color color) => DrawBox(GetEntity(obj), category, xform, dimensions, color);
        public void DrawCapsule(object obj, string category, Point p1, Point p2, float radius, Color color) => DrawCapsule(GetEntity(obj), category, p1, p2, radius, color);
        public void DrawMesh(object obj, string category, Point[] verts, Color color) => DrawMesh(GetEntity(obj), category, verts, color);
        public void DrawLine(object obj, string category, Point p1, Point p2, Color color) => DrawLine(GetEntity(obj), category, p1, p2, color);
        public void DrawCircle(object obj, string category, Point position, Point up, float radius, Color color) => DrawCircle(GetEntity(obj), category, position, up, radius, color);

        #region private

        private List<Entity> Entities = new List<Entity>();
        private Dictionary<object, Entity> EntityMapping = new Dictionary<object, Entity>();
        private int EntityCounter;
        private int FrameCounter;

        private Dictionary<Entity, Transform> LastTransforms = new Dictionary<Entity, Transform>();
        private Dictionary<Entity, Dictionary<string, float>> LastParams = new Dictionary<Entity, Dictionary<string, float>>();

        private bool IsEqual(Point p1, Point p2) => p1.X == p2.X && p1.Y == p2.Y && p1.Z == p2.Z;
        private bool IsEqual(Quaternion q1, Quaternion q2) => q1.X == q2.X && q1.Y == q2.Y && q1.Z == q2.Z && q1.W == q2.W;
        private bool IsEqual(Transform xform1, Transform xform2) => IsEqual(xform1.Translation, xform2.Translation) && IsEqual(xform1.Rotation, xform2.Rotation);

        private BinaryReplayWriter Writer;

        object NullEntityObj = new object();
        private Entity GetEntity(object obj)
        {
            if (obj == null) { return GetEntity(NullEntityObj); }
            if (EntityMapping.TryGetValue(obj, out Entity entity))
            {
                return entity;
            }
            else
            {
                // Auto-create
                RegisterEntity(obj, obj.ToString(), obj.ToString(), obj.GetType().Name, "None", Transform.Identity);
                return EntityMapping[obj];
            }
        }

        private void SetPosition(Entity entity, Point pos)
        {
            if (!LastTransforms.TryGetValue(entity, out Transform last_xform) || !IsEqual(pos, last_xform.Translation))
            {
                Transform xform = last_xform;
                xform.Translation = pos;
                LastTransforms[entity] = xform;
                Writer?.WriteEntitySetPos(entity, FrameCounter, pos);
            }
        }

        private void SetTransform(Entity entity, Transform xform)
        {
            if (!LastTransforms.TryGetValue(entity, out Transform last_xform) || !IsEqual(xform, last_xform))
            {
                LastTransforms[entity] = xform;
                Writer?.WriteEntitySetTransform(entity, FrameCounter, xform);
            }
        }

        private void SetDynamicParam(Entity entity, string key, float val)
        {
            if (!LastParams.TryGetValue(entity, out var paramDict)) { paramDict = new Dictionary<string, float>(); LastParams[entity] = paramDict; }
            if (!paramDict.TryGetValue(key, out float lastValue) || val != lastValue)
            {
                paramDict[key] = val;
                Writer?.WriteEntityValue(entity, FrameCounter, key, val);
            }
        }

        private void SetLog(Entity entity, string category, string log, Color color) => Writer?.WriteEntityLog(entity, FrameCounter, category, log ?? string.Empty, color);
        private void SetDynamicParam(Entity entity, string key, string val) => Writer?.WriteEntityParameter(entity, FrameCounter, key, val ?? string.Empty);
        private void DrawSphere(Entity entity, string category, Point pos, float radius, Color color) => Writer?.WriteEntitySphere(entity, FrameCounter, category, pos, radius, color);
        private void DrawBox(Entity entity, string category, Transform xform, Point dimensions, Color color) => Writer?.WriteEntityBox(entity, FrameCounter, category, xform, dimensions, color);
        private void DrawCapsule(Entity entity, string category, Point p1, Point p2, float radius, Color color) => Writer?.WriteEntityCapsule(entity, FrameCounter, category, p1, p2, radius, color);
        private void DrawMesh(Entity entity, string category, Point[] verts, Color color)
        {
            if (!string.IsNullOrEmpty(category)) throw new NotImplementedException("Mesh draws are only supported at entity creation (category empty)");
            Writer?.WriteEntityMesh(entity, FrameCounter, category, verts, color);
        }
        private void DrawLine(Entity entity, string category, Point p1, Point p2, Color color) => Writer?.WriteEntityLine(entity, FrameCounter, category, p1, p2, color);
        private void DrawCircle(Entity entity, string category, Point position, Point up, float radius, Color color) => Writer?.WriteEntityCircle(entity, FrameCounter, category, position, up, radius, color);

        #endregion //private
    }

    internal class BinaryReplayWriter
    {
        public static System.Text.Encoding StringEncoding => System.Text.Encoding.ASCII;

        BinaryWriterEx _writer;

        public BinaryReplayWriter(Stream stream)
        {
            stream = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionLevel.Fastest);
            _writer = new BinaryWriterEx(stream, StringEncoding);
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
        }

        public void WriteFrameStep(int frame, float totalTime)
        {
            _writer?.Write7BitEncodedInt((int)BlockType.FrameStep);
            //_writer?.Write(frame);
            _writer?.Write(totalTime);
        }

        private void WriteEntityHeader(BlockType blockType, Entity entity, int frame)
        {
            _writer?.Write7BitEncodedInt((int)blockType);
            _writer?.Write7BitEncodedInt(frame);
            _writer?.Write7BitEncodedInt(entity?.Id ?? 0);
        }

        public void WriteEntityDef(Entity entity, int frame)
        {
            WriteEntityHeader(BlockType.EntityDef, entity, frame);
            _writer?.Write(entity);
        }

        public void WriteEntityUndef(Entity entity, int frame)
        {
            WriteEntityHeader(BlockType.EntityUndef, entity, frame);
        }

        public void WriteEntitySetPos(Entity entity, int frame, Point pos)
        {
            WriteEntityHeader(BlockType.EntitySetPos, entity, frame);
            _writer?.Write(pos);
        }

        public void WriteEntitySetTransform(Entity entity, int frame, Transform xform)
        {
            WriteEntityHeader(BlockType.EntitySetTransform, entity, frame);
            _writer?.Write(xform);
        }

        public void WriteEntityLog(Entity entity, int frame, string category, string message, Color color)
        {
            WriteEntityHeader(BlockType.EntityLog, entity, frame);
            _writer?.Write(category);
            _writer?.Write(message);
            _writer?.Write(color);
        }

        public void WriteEntityParameter(Entity entity, int frame, string label, string value)
        {
            WriteEntityHeader(BlockType.EntityParameter, entity, frame);
            _writer?.Write(label);
            _writer?.Write(value);
        }

        public void WriteEntityValue(Entity entity, int frame, string label, float value)
        {
            WriteEntityHeader(BlockType.EntityValue, entity, frame);
            _writer?.Write(label);
            _writer?.Write(value);
        }

        public void WriteEntityLine(Entity entity, int frame, string category, Point start, Point end, Color color)
        {
            WriteEntityHeader(BlockType.EntityLine, entity, frame);
            _writer?.Write(category);
            _writer?.Write(start);
            _writer?.Write(end);
            _writer?.Write(color);
        }

        public void WriteEntityCircle(Entity entity, int frame, string category, Point position, Point up, float radius, Color color)
        {
            WriteEntityHeader(BlockType.EntityCircle, entity, frame);
            _writer?.Write(category);
            _writer?.Write(position);
            _writer?.Write(up);
            _writer?.Write(radius);
            _writer?.Write(color);
        }

        public void WriteEntitySphere(Entity entity, int frame, string category, Point center, float radius, Color color)
        {
            WriteEntityHeader(BlockType.EntitySphere, entity, frame);
            _writer?.Write(category);
            _writer?.Write(center);
            _writer?.Write(radius);
            _writer?.Write(color);
        }

        public void WriteEntityCapsule(Entity entity, int frame, string category, Point p1, Point p2, float radius, Color color)
        {
            WriteEntityHeader(BlockType.EntityCapsule, entity, frame);
            _writer?.Write(category);
            _writer?.Write(p1);
            _writer?.Write(p2);
            _writer?.Write(radius);
            _writer?.Write(color);
        }

        public void WriteEntityMesh(Entity entity, int frame, string category, Point[] verts, Color color)
        {
            WriteEntityHeader(BlockType.EntityMesh, entity, frame);
            _writer?.Write(category);
            _writer?.Write(verts.Length);
            foreach (var vert in verts)
            {
                _writer?.Write(vert);
            }
            _writer?.Write(color);
        }

        public void WriteEntityBox(Entity entity, int frame, string category, Transform xform, Point dimensions, Color color)
        {
            WriteEntityHeader(BlockType.EntityMesh, entity, frame);
            _writer?.Write(category);
            _writer?.Write(xform);
            _writer?.Write(dimensions);
            _writer?.Write(color);
        }
    }

    internal enum BlockType
    {
        None,
        FrameStep,
        EntityDef,
        EntityUndef,
        EntitySetPos,
        EntitySetTransform,
        EntityLog,
        EntityParameter,
        EntityValue,
        EntityLine,
        EntityCircle,
        EntitySphere,
        EntityCapsule,
        EntityMesh,
        EntityBox,

        ReplayHeader = 0xFF
    }

    public class Entity
    {
        public int Id;
        public string Name;
        public string Path;
        public string TypeName;
        public string CategoryName;
        public Transform InitialTransform;
        public Dictionary<string, string> StaticParameters;
        public int CreationFrame;
    }

    internal static class BinaryIOExtensions
    {
        public static void Write(this BinaryWriterEx w, Entity entity)
        {
            w.Write7BitEncodedInt(entity.Id);
            w.Write(entity.Name);
            w.Write(entity.Path);
            w.Write(entity.TypeName);
            w.Write(entity.CategoryName);
            w.Write(entity.InitialTransform);
            w.Write(entity.StaticParameters);
            w.Write7BitEncodedInt(entity.CreationFrame);
        }

        public static void Read(this BinaryReaderEx r, out Entity entity)
        {
            entity = new Entity();
            entity.Id = r.Read7BitEncodedInt();
            entity.Name = r.ReadString();
            entity.Path = r.ReadString();
            entity.TypeName = r.ReadString();
            entity.CategoryName = r.ReadString();
            r.Read(out entity.InitialTransform);
            r.Read(out entity.StaticParameters);
            entity.CreationFrame = r.Read7BitEncodedInt();
        }

        public static void Write(this BinaryWriter w, Point point)
        {
            w.Write(point.X);
            w.Write(point.Y);
            w.Write(point.Z);
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

        public static void Write(this BinaryWriter w, Quaternion quat)
        {
            w.Write(quat.X);
            w.Write(quat.Y);
            w.Write(quat.Z);
            w.Write(quat.W);
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

        public static void Write(this BinaryWriter w, Transform xform)
        {
            w.Write(xform.Translation);
            w.Write(xform.Rotation);
        }

        public static void Read(this BinaryReader r, out Transform xform)
        {
            r.Read(out Point t);
            r.Read(out Quaternion rot);
            xform = new Transform() { Translation = t, Rotation = rot };
        }

        public static void Write(this BinaryWriterEx w, Dictionary<string, string> stringDict)
        {
            w.Write7BitEncodedInt(stringDict.Count);
            foreach (var item in stringDict)
            {
                w.Write(item.Key);
                w.Write(item.Value);
            }
        }

        public static void Read(this BinaryReaderEx r, out Dictionary<string, string> stringDict)
        {
            stringDict = new Dictionary<string, string>();
            int count = r.Read7BitEncodedInt();
            while (count-- > 0)
            {
                stringDict.Add(r.ReadString(), r.ReadString());
            }
        }

        public static void Write(this BinaryWriterEx w, Color color)
        {
            w.Write7BitEncodedInt((int)color);
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
        new public int Read7BitEncodedInt() => base.Read7BitEncodedInt();
    }

    // 7BitEncodedInt marked protected in prior versions of .net
    public class BinaryWriterEx : BinaryWriter
    {
        public BinaryWriterEx(Stream input, System.Text.Encoding encoding) : base(input, encoding) { }
        new public void Write7BitEncodedInt(int val) => base.Write7BitEncodedInt(val);
    }
}
