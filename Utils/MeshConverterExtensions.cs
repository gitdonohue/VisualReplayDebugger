// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System.Linq;

namespace VisualReplayDebugger.Utils;

public static class MeshConverterExtensions
{
    public static System.Windows.Media.Media3D.MeshGeometry3D ConvertToWndMeshGeometry3D(this HelixToolkit.Geometry.MeshGeometry3D helixMesh)
    {
        var wpfMesh = new System.Windows.Media.Media3D.MeshGeometry3D();

        wpfMesh.Positions = new(helixMesh.Positions.Select(p => new System.Windows.Media.Media3D.Point3D(p.X, p.Y, p.Z)));
        wpfMesh.TriangleIndices = new(helixMesh.TriangleIndices);
        
        if (helixMesh.Normals != null) 
        {
            wpfMesh.Normals = new(helixMesh.Normals.Select(n => new System.Windows.Media.Media3D.Vector3D(n.X, n.Y, n.Z)));
        }

        if (helixMesh.TextureCoordinates != null)
        {
            wpfMesh.TextureCoordinates = new(helixMesh.TextureCoordinates.Select(c => new System.Windows.Point(c.X,c.Y)));
        }

        wpfMesh.Freeze();
        return wpfMesh;
    }
}
