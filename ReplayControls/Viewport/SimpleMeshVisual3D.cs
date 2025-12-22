// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using HelixToolkit.Geometry;
using HelixToolkit.Wpf;
using System.Collections.Generic;
using System.Linq;
using VisualReplayDebugger.Utils;
using Point3D = System.Windows.Media.Media3D.Point3D;

namespace HelixToolkit.Wpf
{
    public class SimpleMeshVisual3D : MeshElement3D
    {

        private Point3D[] _verts;
        public SimpleMeshVisual3D(IEnumerable<Point3D> verts) : base()
        {
            _verts = verts.ToArray();
            UpdateModel();
        }

        protected override System.Windows.Media.Media3D.MeshGeometry3D Tessellate()
        {
            var builder = new MeshBuilder(false, false);
            if (_verts != null)
            {
                //builder.Append(_verts, Enumerable.Range(0, _verts.Length).ToArray());
            }
            return builder.ToMesh().ConvertToWndMeshGeometry3D();
        }
    }
}
