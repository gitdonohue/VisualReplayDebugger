// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using _3DTools;
using HelixToolkit.Wpf;
using ReplayCapture;
using SelectionSet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Timeline;
using TimelineControls;
using WatchedVariable;
using static ReplayCapture.ReplayCaptureReader;

namespace VisualReplayDebugger
{
    public class ReplayViewportContent // TODO: Make IDisposable
    {
        public ModelVisual3D ModelVisual3D { get; private set; }
        public Model3DGroup Model3DGroup { get; private set; }
        public Viewport3D Viewport3D { get; private set; }

        ReplayCaptureReader replay;
        public ReplayCaptureReader Replay 
        { 
            get => replay; 
            set
            {
                replay = value;
                LoadReplay();
            }
        }

        public TimelineWindow TimelineWindow { get; private set; }

        ForDict<Entity, List<(Transform3D,ModelVisual3D)>> EntityModels = new();
        Dictionary<Entity, ScreenSpaceLines3D> EntityPaths = new();

        Dictionary<Model3D, Entity> EntityModelsIndex = new();

        Entity CameraEntity;
        ModelVisual3D CameraIndicator;

        public event Action<string, Point3D, int> DrawLabelRequest;
        public event Action DrawLabelResetRequest;

        public event Action<Point3D, int, System.Windows.Media.Color> DrawCircleRequest;
        public event Action DrawCircleResetRequest;

        public event Action CameraManuallyMoved;

        public event Action<Point3D> FocusAtRequested;

        public SelectionGroup<Entity> EntitySelection { get; private set; }
        public SelectionGroup<Entity> VisibleEntities { get; private set; }

        public SelectionGroup<string> DrawCategoryFilter { get; private set; }
        public SelectionGroup<ReplayCapture.Color> DrawColorFilter { get; private set; }

        public WatchedBool FollowCameraEnabled { get; } = new();
        public WatchedBool FollowSelectionEnabled { get; } = new();
        public WatchedBool ShowAllNames { get; } = new(true);
        public WatchedBool ShowAllPaths { get; } = new(true);
        public WatchedBool ShowDrawPrimitives { get; } = new(true);
        public WatchedBool ShowAllDrawPrimitivesInRange { get; } = new(true);
        public ReplayViewportContent(Viewport3D viewport, ReplayCaptureReader replay, TimelineWindow timelinewindow, SelectionGroup<Entity> selectionset, SelectionGroup<Entity> visibleEntities,
            SelectionGroup<string> drawCategoryFilter, SelectionGroup<ReplayCapture.Color> drawColorFilter)
        {
            Viewport3D = viewport;
            TimelineWindow = timelinewindow;
            ModelVisual3D = new ModelVisual3D();
            Model3DGroup = new Model3DGroup();
            ModelVisual3D.Content = Model3DGroup;

            //Timeline.Changed += Timeline_Changed;
            EntitySelection = selectionset;
            EntitySelection.Changed += () => { RecalcGeometry(); SetTime(TimelineWindow.Timeline.Cursor); }; // TODO: Make IDiposable

            VisibleEntities = visibleEntities;
            VisibleEntities.Changed += VisibleEntities_Changed;

            DrawCategoryFilter = drawCategoryFilter;
            DrawCategoryFilter.Changed += VisibleEntities_Changed;
            DrawColorFilter = drawColorFilter;
            DrawColorFilter.Changed += VisibleEntities_Changed;

            viewport.Camera.Changed += Camera_Changed;
            TimelineWindow.Changed += Timeline_Changed;

            FollowCameraEnabled.Changed += Timeline_Changed;
            ShowAllNames.Changed += Timeline_Changed;
            ShowAllPaths.Changed += Redraw;
            ShowDrawPrimitives.Changed += Redraw;
            ShowAllDrawPrimitivesInRange.Changed += Redraw;
            this.Viewport3D.IsVisibleChanged += (o, e) => Redraw(); // TODO: Make IDiposable

            Replay = replay;
        }

        private ScreenSpaceLines3D CreateLineGroup(System.Windows.Media.Color color, double thickness = 2.0)
        {
            var linegroup = new ScreenSpaceLines3D() { GetVisualToViewportTransform = () => Viewport3D.GetTotalTransform() };
            linegroup.Color = color;
            linegroup.Thickness = thickness;
            linegroup.Points = new Point3DCollection();
            Model3DGroup.Children.Add(linegroup.Content);
            return linegroup;
        }

        private void VisibleEntities_Changed()
        {
            foreach (var kv in EntityModels)
            {
                var entity = kv.Key;
                bool isVisible = VisibleEntities.Contains(entity);
                foreach ((var xform,var geom) in kv.Value)
                {
                    Model3D model = geom.Content;
                    bool isModelCurrentlyVisible = Model3DGroup.Children.Contains(model);

                    if ( !DrawColorFilter.Empty )
                    {
                        MeshElement3D meshgeom = geom as MeshElement3D;
                        if (meshgeom != null)
                        {
                            var col = MaterialsForColors.Where(x => x.Value == meshgeom.Material).First().Key;
                            if (DrawColorFilter.Contains(col))
                            {
                                isVisible = false;
                            }
                        }
                    }

                    if (isVisible && !isModelCurrentlyVisible)
                    {
                        Model3DGroup.Children.Add(geom.Content);
                    }
                    else if (!isVisible && isModelCurrentlyVisible)
                    {
                        Model3DGroup.Children.Remove(geom.Content);
                    }
                }
            }
            Redraw();
        }

        public void OnMouseDown(System.Windows.Point mousePos)
        {
            // Picking
            // TODO: Fix issue with sometimes needing to have a clear selection for picking to work correctly
            var hitParams = new PointHitTestParameters(mousePos);
            VisualTreeHelper.HitTest(Viewport3D,
                (cb) =>
                {
                    if (cb is GridLinesVisual3D) return HitTestFilterBehavior.ContinueSkipSelfAndChildren;
                    return HitTestFilterBehavior.Continue;
                },
                (cb) =>
                {
                    var hitresult = cb as RayMeshGeometry3DHitTestResult;
                    var geom = hitresult.ModelHit;
                    //var geom = hitModel.Content as GeometryModel3D;
                    if (geom != null && EntityModelsIndex.TryGetValue(geom, out Entity entity))
                    {
                        if (Keyboard.IsKeyDown(Key.LeftCtrl))
                        {
                            if (EntitySelection.Contains(entity))
                            {
                                EntitySelection.Remove(entity);
                            }
                            else
                            {
                                EntitySelection.Add(entity);
                            }
                        }
                        else
                        {
                            EntitySelection.Set(entity);
                        }
                        return HitTestResultBehavior.Stop;
                    }
                    return HitTestResultBehavior.Continue;
                },
                hitParams);
        }

        Dictionary<ReplayCapture.Color, ScreenSpaceLines3D> LinesByColor = new();
        Dictionary<ReplayCapture.Color, System.Windows.Media.Color> ColorConversion = new();

        private void DrawLinesClear()
        { 
            foreach (var linegroup in LinesByColor.Values) { linegroup.Points.Clear(); } 
        }

        private void DrawLine(Point p1, Point p2, ReplayCapture.Color color)
        {
            var drawpoints = LinesByColor[color].Points;
            drawpoints.Add(p1.ToPoint());
            drawpoints.Add(p2.ToPoint());
        }

        Dictionary<ReplayCapture.Color, System.Windows.Media.Media3D.Material> MaterialsForColors = new();

        
        List<MeshElement3D> drawnSpheres = new();
        private void DrawSpheresClear()
        {
            foreach (var s in drawnSpheres)
            {
                s.Visible = false;
            }
        }

        List<CapsuleVisual3D> capsulesCache = new();
        HashSet<CapsuleVisual3D> availableCapsules = new();
        private void DrawCapsulesPre()
        {
            availableCapsules.Clear();
            foreach (var c in capsulesCache) { availableCapsules.Add(c); }
        }
        private void DrawCapsulesPost()
        {
            // Hide unused at end of draw
            foreach(var c in capsulesCache)
            {
                if (availableCapsules.Contains(c) && c.Visible) 
                { 
                    c.Visible = false; 
                }
            }
        }

        private void DrawSphere(Point p, double radius, ReplayCapture.Color color)
        {
            var sphere = drawnSpheres.FirstOrDefault(x => !x.Visible);
            if (sphere == null)
            {
                sphere = new SphereVisual3D() { Radius = 1, PhiDiv = 10, ThetaDiv = 20 };
                Model3DGroup.Children.Add(sphere.Content);
                drawnSpheres.Add(sphere);
            }

            var pos = p.ToPoint();
            var posxform = new TranslateTransform3D() { OffsetX = pos.X, OffsetY = pos.Y, OffsetZ = pos.Z };
            var scalexform = new ScaleTransform3D(radius, radius, radius);
            sphere.Model.Transform = new MatrixTransform3D(scalexform.Value * posxform.Value);
            sphere.Material = MaterialsForColors[color];
            sphere.Visible = true;
        }

        private void DrawBox(ReplayCapture.Transform xfrom, Point dimensions, ReplayCapture.Color color)
        {
            var box = new BoxVisual3D() { Width = dimensions.X, Length = dimensions.Y, Height = dimensions.Z };
            Model3DGroup.Children.Add(box.Content);
            box.Model.Transform = xfrom.ToTransform3D();
            box.Material = MaterialsForColors[color];
            box.Visible = true;
        }

        private void DrawCapsule(Point p1, Point p2, double radius, ReplayCapture.Color color) => DrawCapsule(p1.ToPoint(), p2.ToPoint(), radius, color);
        private void DrawCapsule(Point3D p1, Point3D p2, double radius, ReplayCapture.Color color)
        {
            var capsule = availableCapsules.FirstOrDefault(x => x.SimilarTo(p2 - p1, radius));
            if (capsule != null)
            {
                availableCapsules.Remove(capsule);
            }
            else
            {
                capsule = new CapsuleVisual3D() { End = (p2 - p1).ToPoint3D(), Radius = radius, PhiDiv = 10, ThetaDiv = 20 };
                Model3DGroup.Children.Add(capsule.Content);
                capsulesCache.Add(capsule);
            }
            capsule.Model.Transform = new TranslateTransform3D() { OffsetX = p1.X, OffsetY = p1.Y, OffsetZ = p1.Z };
            capsule.Material = MaterialsForColors[color];
            capsule.Visible = true;
        }

        private void LoadReplay()
        {
            Model3DGroup.Children.Clear();
            EntityPaths.Clear();
            EntityModels.Clear();
            CameraIndicator = null;
            CameraEntity = null;

            MaterialsForColors.Clear();
            foreach (ReplayCapture.Color color in Enum.GetValues(typeof(ReplayCapture.Color)))
            {
                var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(color.ToString());
                ColorConversion[color] = c;
                LinesByColor[color] = CreateLineGroup(c);
                MaterialsForColors[color] = MaterialHelper.CreateMaterial(new SolidColorBrush(c), specularPower: 50);
            }

            foreach (var linegroup in LinesByColor.Values) { linegroup.Points.Clear(); }

            if (Replay == null) return;

            // draws commands without a category and sent at entity creation are considered to be the visual representation of the object
            bool showEntityVisualRep = true;
            if (showEntityVisualRep)
            {
                foreach (var entity in Replay.Entities)
                {
                    int creationFrame = Replay.GetEntityLifeTime(entity).Start;
                    var creationDrawCommands = Replay.DrawCommands.AtFrame(creationFrame).Where(x=>x.entity == entity && x.IsCreationDraw);
                    foreach (var creationDrawCommand in creationDrawCommands)
                    {
                        var drawXform = creationDrawCommand.xform.ToTransform3D();
                        var entityXform = entity.InitialTransform.ToTransform3D();

                        // Object space xform
                        var entity_space_offset =  creationDrawCommand.xform.Translation.ToPoint() - entity.InitialTransform.Translation.ToPoint();
                        Transform3D modelTransform = new TranslateTransform3D(entity_space_offset);
                        // TODO: Handle rotation

                        ModelVisual3D geom = null;
                        if (creationDrawCommand.type == EntityDrawCommandType.Line)
                        {
                            //// Lines are not aligning well in 3d after transform.
                            //var lines = new ScreenSpaceLines3D() { GetVisualToViewportTransform = () => Viewport3D.GetTotalTransform() };
                            //lines.Points.Add((creationDrawCommand.Pos.ToPoint() - entity.InitialTransform.Translation.ToPoint()).ToPoint3D());
                            //lines.Points.Add((creationDrawCommand.p2.ToPoint() - entity.InitialTransform.Translation.ToPoint()).ToPoint3D());
                            //lines.Thickness = 2.0;
                            //lines.Color = ColorConversion[creationDrawCommand.color];
                            //geom = lines;
                        }
                        else if (creationDrawCommand.type == EntityDrawCommandType.Sphere)
                        {
                            geom = new SphereVisual3D() { Radius = creationDrawCommand.Radius, PhiDiv = 10, ThetaDiv = 20 };
                        }
                        else if (creationDrawCommand.type == EntityDrawCommandType.Box)
                        {
                            geom = new BoxVisual3D() { Width = creationDrawCommand.Dimensions.X, Length = creationDrawCommand.Dimensions.Y, Height = creationDrawCommand.Dimensions.Z };
                        }
                        else if (creationDrawCommand.type == EntityDrawCommandType.Capsule)
                        {
                            geom = new CapsuleVisual3D() { End = (creationDrawCommand.EndPoint.ToPoint() - creationDrawCommand.Pos.ToPoint()).ToPoint3D(), Radius = creationDrawCommand.Radius, PhiDiv = 10, ThetaDiv = 20 };
                        }
                        else if (creationDrawCommand.type == EntityDrawCommandType.Mesh)
                        {
                            // MeshVisual3D makes the viewport lag.
                            //var meshDef = new Mesh3D(creationDrawCommand.verts.Select(x=>x.ToPoint()), Enumerable.Range(0, creationDrawCommand.verts.Length));
                            //geom = new MeshVisual3D() { Mesh = meshDef, VertexResolution = 0 };

                            // Much faster, but not as pretty
                            geom = new SimpleMeshVisual3D(creationDrawCommand.verts.Select(x => x.ToPoint()));
                        }

                        MeshElement3D meshgeom = geom as MeshElement3D;
                        if (meshgeom != null)
                        {
                            meshgeom.Material = MaterialsForColors[creationDrawCommand.color];
                        }

                        if (geom != null)
                        {
                            if (!Replay.EntitiesWithTransforms.Contains(entity))
                            {
                                geom.Content.Freeze();
                            }

                            EntityModels.For(entity).Add((modelTransform,geom));
                            Model3DGroup.Children.Add(geom.Content);
                            EntityModelsIndex.Add(geom.Content, entity);
                        }
                    }
                }
            }

            foreach (var entities in Replay.EntitiesWithTransforms)
            {
                var pathLines = CreateLineGroup(Colors.Gray);
                EntityPaths.Add(entities,pathLines);
            }

            CameraEntity = Replay.Entities.FirstOrDefault(e => e.Name == "maincamera");
            if (CameraEntity != null)
            {
                var camPos = new CoordinateSystemVisual3D();
                ModelVisual3D.Children.Add(camPos);
                CameraIndicator = camPos;
            }

            LinesByColor.Clear();
            foreach (ReplayCapture.Color color in Enum.GetValues(typeof(ReplayCapture.Color)))
            {
                var c = (System.Windows.Media.Color)ColorConverter.ConvertFromString(color.ToString());
                LinesByColor[color] = CreateLineGroup(c);
            }

            // Fit timeline to replay
            TimelineWindow.Timeline.End = Replay.TotalTime;
            TimelineWindow.Fill();

            RecalcGeometry();
        }

        private void RecalcGeometry()
        {
            if (Replay == null) return;
            if (!this.Viewport3D.IsVisible) return;
            
            DrawLinesClear();
            DrawSpheresClear();
            DrawCapsulesPre();

            int cursorFrame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
            var windowRange = Replay.GetFramesForTimes(TimelineWindow.Start, TimelineWindow.End);
            foreach (var kvp in EntityPaths)
            {
                var entity = kvp.Key;
                var pathLines = kvp.Value;

                bool isEntityInSelection = EntitySelection.SelectionSet.Contains(entity);

                pathLines.Color = (ShowAllPaths && isEntityInSelection) ? Colors.Red : Colors.Gray;
                pathLines.Points.Clear();

                if (ShowAllPaths || EntitySelection.SelectionSet.Contains(entity))
                {
                    var lifetime = Replay.GetEntityLifeTime(entity);
                    if (lifetime.Overlaps(windowRange))
                    {
                        var positions = Replay.GetEntityTransforms(entity);
                        var points = pathLines.Points;

                        // Show window only
                        var start = positions.FirstAtFrame(windowRange.Start);
                        points.Add(new Point3D(start.Translation.X, start.Translation.Y, start.Translation.Z));
                        foreach (var timedPos in positions)
                        {
                            if (windowRange.InRange(timedPos.Item1))
                            {
                                points.Add(timedPos.Item2.Translation.ToPoint());
                                points.Add(timedPos.Item2.Translation.ToPoint());
                            }
                        }
                    }
                }
            }

            if (ShowDrawPrimitives)
            {
                var drawCommands = ShowAllDrawPrimitivesInRange ? Replay.DrawCommands.SubRange(windowRange) : Replay.DrawCommands.ForFrame(cursorFrame);

                // Note: what about draw commands with non-zero durations?
                foreach ((int _, EntityDrawCommand cmd) in drawCommands)
                {
                    if (cmd.IsCreationDraw) continue;

                    if (!DrawCategoryFilter.Empty && DrawCategoryFilter.Contains(cmd.category)) continue;
                    if (!DrawColorFilter.Empty && DrawColorFilter.Contains(cmd.color)) continue;

                    switch (cmd.type)
                    {
                        case EntityDrawCommandType.Line:
                            {
                                DrawLine(cmd.Pos, cmd.EndPoint, cmd.color);
                            }
                            break;
                        case EntityDrawCommandType.Sphere:
                            {
                                DrawSphere(cmd.Pos, cmd.Radius, cmd.color);
                            }
                            break;
                        case EntityDrawCommandType.Box:
                            {
                                DrawBox(cmd.xform, cmd.p2, cmd.color);
                            }
                            break;
                        case EntityDrawCommandType.Capsule:
                            {
                                DrawCapsule(cmd.Pos, cmd.EndPoint, cmd.Radius, cmd.color);
                            }
                            break;
                    }
                }
            }

            DrawCapsulesPost();
        }

        private void Redraw()
        {
            RecalcGeometry();
            SetTime(TimelineWindow.Timeline.Cursor);
        }

        double lastStart;
        double lastEnd;
        private void Timeline_Changed()
        {
            bool timelineWindowRangeChanged = (lastStart != TimelineWindow.Start || lastEnd != TimelineWindow.End);
            lastStart = TimelineWindow.Start;
            lastEnd = TimelineWindow.End;
            if (timelineWindowRangeChanged || !ShowAllDrawPrimitivesInRange)
            {
                RecalcGeometry();
            }

            SetTime(TimelineWindow.Timeline.Cursor);
        }

        private bool IsCameraBeingModified;
        private void Camera_Changed(object sender, EventArgs e)
        {
            if (!IsCameraBeingModified)
            {
                if (FollowCameraEnabled) { FollowCameraEnabled.Set(false); }
                CameraManuallyMoved?.Invoke();
            }
        }

        public void SetTime(double time)
        {
            if (Replay == null) return;

            int frame = Replay.GetFrameForTime(time);
            DrawLabelResetRequest?.Invoke();
            DrawCircleResetRequest?.Invoke();
            foreach (var entity in Replay.EntitiesWithTransforms)
            {
                bool isAlive = Replay.GetEntityLifeTime(entity).InRange(frame);
                bool isSelected = EntitySelection.SelectionSet.Contains(entity);

                var pos = Replay.GetEntityPosition(entity, frame);
                if (EntityModels.TryGetValue(entity, out var geomlist))
                {
                    var entityTransform = new TranslateTransform3D() { OffsetX = pos.X, OffsetY = pos.Y, OffsetZ = pos.Z }.Value;
                    foreach ((Transform3D localXform, ModelVisual3D geom) in geomlist)
                    {
                        var finalTransform = new MatrixTransform3D(entityTransform * localXform.Value);
                        if (geom is MeshElement3D meshgeom)
                        {
                            meshgeom.Visible = isAlive;
                            if (isAlive)
                            {

                                meshgeom.Model.Transform = finalTransform;
                            }
                        }
                        else if (geom is ScreenSpaceLines3D lines)
                        {
                            lines.Content.Transform = finalTransform;
                        }
                    }
                }

                //if (EntitySelection.Contains(entity))
                //{
                //    //var txt = string.Join('\n', Replay.EntityDynamicParams.For(entity).AtFrame(frame).Select(x => $"{x.Item1}:{x.Item2}"));
                //    var txt = "\n"+string.Join('\n', Replay.GetDynamicParamsAt(entity, frame).Select(x => $"{x.Key}:{x.Value}"));
                //    DrawLabelRequest?.Invoke(txt, pos.ToPoint(), 10);
                //}

                if (isAlive && (ShowAllNames || isSelected))
                {
                    DrawLabelRequest?.Invoke(entity.Name, pos.ToPoint(), 12);
                }
                //var text = EntityLabels[entity];
                //text.Transform = new TranslateTransform3D() { OffsetX = pos.X, OffsetY = pos.Y, OffsetZ = pos.Z };
                //text.DepthOffset = geom.Visible ? 0.001 : 0.01;

                if (isAlive)
                {
                    DrawCircleRequest?.Invoke(pos.ToPoint(), 8, isSelected ? Colors.Red : Colors.Blue);
                }
            }

            // Follow Cam
            if (FollowCameraEnabled && CameraEntity != null && CameraIndicator != null)
            {
                var xform = Replay.GetEntityTransform(CameraEntity, frame).ToTransform3D();
                CameraIndicator.Transform = xform;

                IsCameraBeingModified = true;
                var cam = (Viewport3D.Camera as PerspectiveCamera);
                cam.FieldOfView = 60;
                cam.Position = xform.Transform(new Point3D());
                cam.UpDirection = new Vector3D(0, 1, 0);
                cam.LookDirection = xform.Transform(new Vector3D(0, 0, -1));
                IsCameraBeingModified = false;
            }

            if (FollowSelectionEnabled)
            {
                FocusOnSelection();
            }
        }

        public void FocusOnSelection()
        {
            if (!EntitySelection.Empty)
            {
                int frame = Replay.GetFrameForTime(TimelineWindow.Timeline.Cursor);
                Vector3D avgPos = new();
                foreach (var entity in EntitySelection.SelectionSet)
                {
                    avgPos += Replay.GetEntityPosition(entity,frame).ToPoint().ToVector3D();
                }
                avgPos /= EntitySelection.SelectionSet.Count;

                //FollowCameraEnabled.Set(false);
                //IsCameraBeingModified = true;
                //var cam = (Viewport3D.Camera as PerspectiveCamera);
                // TODO: Move the camera on the plane defined by the up vector so that it aligns with the point
                //IsCameraBeingModified = false;

                FocusAtRequested?.Invoke(avgPos.ToPoint3D());
            }
        }
    }

    public static class ReplayViewportExtensions
    {
        public static Point3D ToPoint(this ReplayCapture.Point p) => new Point3D(p.X, p.Y, p.Z);
        public static Transform3D ToTransform3D(this ReplayCapture.Transform xform) =>
            Transform3DHelper.CombineTransform(
                new RotateTransform3D(new QuaternionRotation3D(new System.Windows.Media.Media3D.Quaternion(xform.Rotation.X, xform.Rotation.Y, xform.Rotation.Z, xform.Rotation.W))),
                new TranslateTransform3D(xform.Translation.X, xform.Translation.Y, xform.Translation.Z));
    }
}
