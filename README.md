# VisualReplayDebugger
Visual game replay debugging tool.

Inspired by physics replay debugging toos like the PhysX and Havok Visual Debuggers, as well as the Unreal Visual Logger.
The tool is engine agnostic, the user must explicitly define what gets serialized.
The main use-case is for debugging gameplay and AI.

![image](https://user-images.githubusercontent.com/44268295/137595268-1eddf06a-293e-4743-9abc-3efb9a5ccc33.png)

The tool can be installed via: https://www.microsoft.com/store/apps/9NT19LVXQS0F

To integrate into your app/game, you can use the following serializers:

[C# serializer](Replay/Serializers/dotnet/ReplayCapture.cs)

[C serializer](Replay/Serializers/c/ReplayCapture.h)

(more to come...)

Replay session serialization allows the following operations:

``` C#
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
```

The replay serialization format is structured in a way that the file stream does not need to be closed in order for the file to be valid.

This is a work in progress.
