// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using HelixToolkit.Geometry;

using System.Windows;
using VisualReplayDebugger.Utils;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace HelixToolkit.Wpf
{
    public class CapsuleVisual3D : SphereVisual3D
    {
        public CapsuleVisual3D() : base()
        {

        }

        /// <summary>
        /// Identifies the <see cref="End"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
            "End",
            typeof(Point3D),
            typeof(SphereVisual3D),
            new PropertyMetadata(new Point3D(0, 0, 0), GeometryChanged));

        /// <summary>
        /// Gets or sets the center of the sphere.
        /// </summary>
        /// <value>The center.</value>
        public Point3D End
        {
            get
            {
                return (Point3D)this.GetValue(EndProperty);
            }

            set
            {
                this.SetValue(EndProperty, value);
            }
        }

        public Point3D Start { get => Center; set => Center = value; }

        public bool SimilarTo(Point3D p1, Point3D p2, double radius) => SimilarTo(p2-p1, radius);
        public bool SimilarTo(Vector3D h, double radius) => (h - (End - Center)).Length < float.Epsilon;

        protected override System.Windows.Media.Media3D.MeshGeometry3D Tessellate()
        {
            // TODO: make an actual capsule (vs 2 spheres and a cylinder)
            var builder = new MeshBuilder(true, true);
            builder.AddSphere(this.Center.ToVector3(), (float)this.Radius, this.ThetaDiv, this.PhiDiv);
            builder.AddCylinder(this.Center.ToVector3(), this.End.ToVector3(), (float)this.Radius, this.ThetaDiv, cap1: false, cap2: false);
            builder.AddSphere(this.End.ToVector3(), (float)this.Radius, this.ThetaDiv, this.PhiDiv);
            return builder.ToMesh().ConvertToWndMeshGeometry3D();
        }
    }
}
