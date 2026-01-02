// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using HelixToolkit.Geometry;

using System.Windows;
using VisualReplayDebugger.Utils;
using Point3D = System.Windows.Media.Media3D.Point3D;
using Vector3D = System.Windows.Media.Media3D.Vector3D;

namespace HelixToolkit.Wpf
{
    public class CapsuleVisual3D : MeshElement3D
    {
        public CapsuleVisual3D() : base()
        {

        }

        /// <summary>
        /// Identifies the <see cref="Start"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty StartProperty = DependencyProperty.Register(
            nameof(Start),
            typeof(Point3D),
            typeof(CapsuleVisual3D),
            new PropertyMetadata(new Point3D(0, 0, 0), GeometryChanged));

        /// <summary>
        /// Gets or sets the center of the sphere.
        /// </summary>
        /// <value>The center.</value>
        public Point3D Start
        {
            get
            {
                return (Point3D)this.GetValue(StartProperty);
            }

            set
            {
                this.SetValue(StartProperty, value);
            }
        }

        /// <summary>
        /// Identifies the <see cref="End"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EndProperty = DependencyProperty.Register(
            nameof(End),
            typeof(Point3D),
            typeof(CapsuleVisual3D),
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

        /// <summary>
        /// Identifies the <see cref="Radius"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
            nameof(Radius),
            typeof(double),
            typeof(CapsuleVisual3D),
            new PropertyMetadata(1.0, GeometryChanged));

        /// <summary>
        /// Gets or sets the radius of the capsule.
        /// </summary>
        /// <value>The radius.</value>
        public double Radius
        {
            get
            {
                return (double)this.GetValue(RadiusProperty);
            }

            set
            {
                this.SetValue(RadiusProperty, value);
            }
        }

        int ThetaDiv = 16;
        int PhiDiv = 8;

        protected override System.Windows.Media.Media3D.MeshGeometry3D Tessellate()
        {
            // TODO: make an actual capsule (vs 2 spheres and a cylinder)
            var builder = new MeshBuilder(true, true);
            builder.AddSphere(Start.ToVector3(), (float)Radius, ThetaDiv, PhiDiv);
            builder.AddCylinder(Start.ToVector3(), End.ToVector3(), (float)Radius, ThetaDiv, cap1: false, cap2: false);
            builder.AddSphere(End.ToVector3(), (float)Radius, ThetaDiv, PhiDiv);
            return builder.ToMesh().ConvertToWndMeshGeometry3D();
        }
    }
}
