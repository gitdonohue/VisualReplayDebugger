using ReplayCapture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VisualReplayDebugger
{
    class SerializeTest
    {
        public static string TestReplayFileExport()
        {
            string output_filename = "test_output.vrd";
            ReplayCaptureWriter replay_writer = new(output_filename);

            // Test object registration
            object testobj1 = new();
            replay_writer.RegisterEntity(testobj1, "testobj1", "root/testobj1", "object", "Objects", Transform.Identity);

            // Test object visual rep
            replay_writer.DrawSphere(testobj1, string.Empty, new Point() { X = 0, Y = 0, Z = 0 }, 0.25f, Color.GreenYellow);

            // Test static params
            object testobj2 = new();
            var object2_staticParams = new Dictionary<string, string>
            {
                { "key1", "value1" },
                { "key2", "value2" }
            };
            replay_writer.RegisterEntity(testobj2, "testobj2", "root/testobj2", "object", "Objects", Transform.Identity, object2_staticParams);

            // Test draws
            replay_writer.DrawLine(testobj1, "drawtests", new Point() { X = 1, Y = 1, Z = 1 }, new Point() { X = 2, Y = 2, Z = 2 }, Color.CadetBlue);
            replay_writer.DrawSphere(testobj1, "drawtests", new Point() { X = 3, Y = 3, Z = 3 }, 0.5f, Color.Bisque);
            replay_writer.DrawCapsule(testobj1, "drawtests", new Point() { X = 4, Y = 4, Z = 4 }, new Point() { X = 5, Y = 5, Z = 5 }, 0.5f, Color.Chartreuse);
            //replay_writer.DrawBox(testobj1, "drawtests", new Transform() { Translation = new Point() { X = 6, Y = 6, Z = 6 }, Rotation = Quaternion.Identity }, new Point() { X = 0.5f, Y = 0.5f, Z = 0.5f }, Color.DarkOrchid);

            object meshObj = new();
            replay_writer.RegisterEntity(meshObj, "meshObj", "root/meshObj", "object", "Objects", Transform.Identity);
            Point[] pts = new[] { 
                new Point() { X = 0, Y = 0, Z = 0 },
                new Point() { X = 1, Y = 0, Z = 0 }, 
                new Point() { X = 1, Y = 1, Z = 0 },

                new Point() { X = 0, Y = 0, Z = 0 },
                new Point() { X = 1, Y = 1, Z = 0 },
                new Point() { X = 0, Y = 1, Z = 0 }
            };
            replay_writer.DrawMesh(meshObj, string.Empty, pts, Color.ForestGreen);

            float totalTime = 0;
            int stepCount = 1001;
            for (int i = 0; i < stepCount; ++i)
            {
                replay_writer.StepFrame(totalTime);

                replay_writer.SetLog(testobj1, "no_category", $"Logging at step {i}", Color.Blue);

                float y = (float)Math.Abs(Math.Sin(totalTime * 3.14 / 10));
                replay_writer.SetDynamicParam(testobj1, "sinabs", y);

                var testObjPos = new Point() { X = totalTime, Y = y, Z = 0 };
                replay_writer.SetPosition(testobj1, testObjPos);

                totalTime += 1.0f / 30;
            }

            replay_writer.Dispose();
            return output_filename;
        }
    }
}
