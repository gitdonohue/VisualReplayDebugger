// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using HelixToolkit.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Media3D;

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

        protected override MeshGeometry3D Tessellate()
        {
            var builder = new MeshBuilder(false, false);
            if (_verts != null)
            {
                builder.Append(_verts, Enumerable.Range(0, _verts.Length).ToArray());
            }
            return builder.ToMesh();
        }
    }
}
