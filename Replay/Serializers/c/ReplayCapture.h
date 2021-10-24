#ifndef REPLAYCAPTURE_H
#define REPLAYCAPTURE_H

#ifdef __cplusplus
extern "C" {
#endif

// Note: define VRD_USE_ZLIB and link with zlib (static or dll), in order to get more compact file (5:1 approx)

typedef struct VRD_replay_context_s VRD_replay_context;
enum VRD_Color { AliceBlue, PaleGoldenrod, Orchid, OrangeRed, Orange, OliveDrab, Olive, OldLace, Navy, NavajoWhite, Moccasin, MistyRose, MintCream, MidnightBlue, MediumVioletRed, MediumTurquoise, MediumSpringGreen, MediumSlateBlue, LightSkyBlue, LightSlateGray, LightSteelBlue, LightYellow, Lime, LimeGreen, PaleGreen, Linen, Maroon, MediumAquamarine, MediumBlue, MediumOrchid, MediumPurple, MediumSeaGreen, Magenta, PaleTurquoise, PaleVioletRed, PapayaWhip, SlateGray, Snow, SpringGreen, SteelBlue, Tan, Teal, SlateBlue, Thistle, Transparent, Turquoise, Violet, Wheat, White, WhiteSmoke, Tomato, LightSeaGreen, SkyBlue, Sienna, PeachPuff, Peru, Pink, Plum, PowderBlue, Purple, Silver, Red, RoyalBlue, SaddleBrown, Salmon, SandyBrown, SeaGreen, SeaShell, RosyBrown, Yellow, LightSalmon, LightGreen, DarkRed, DarkOrchid, DarkOrange, DarkOliveGreen, DarkMagenta, DarkKhaki, DarkGreen, DarkGray, DarkGoldenrod, DarkCyan, DarkBlue, Cyan, Crimson, Cornsilk, CornflowerBlue, Coral, Chocolate, AntiqueWhite, Aqua, Aquamarine, Azure, Beige, Bisque, DarkSalmon, Black, Blue, BlueViolet, Brown, BurlyWood, CadetBlue, Chartreuse, BlanchedAlmond, DarkSeaGreen, DarkSlateBlue, DarkSlateGray, HotPink, IndianRed, Indigo, Ivory, Khaki, Lavender, Honeydew, LavenderBlush, LemonChiffon, LightBlue, LightCoral, LightCyan, LightGoldenrodYellow, LightGray, LawnGreen, LightPink, GreenYellow, Gray, DarkTurquoise, DarkViolet, DeepPink, DeepSkyBlue, DimGray, DodgerBlue, Green, Firebrick, ForestGreen, Fuchsia, Gainsboro, GhostWhite, Gold, Goldenrod, FloralWhite, YellowGreen };

typedef struct VRD_Point_s { float x, y, z; } VRD_Point;
typedef struct VRD_Quaternion_s { float x, y, z, w; } VRD_Quaternion;
typedef struct VRD_Transform_s { VRD_Point translation; VRD_Quaternion rotation; } VRD_Transform;
typedef struct VRD_StringDictPair_s { const char *key, *value; } VRD_StringDictPair;

VRD_replay_context* VRD_CreateContext(const char* filename, int compressed);
void VRD_ReleaseContext(VRD_replay_context* ctx);
void VRD_RegisterEntity(VRD_replay_context* ctx, long entityId, const char* name, const char* path, const char* type_name, const char* category_name, VRD_Transform* xform, VRD_StringDictPair* staticParams, int staticParamsCount);
void VRD_UnRegisterEntity(VRD_replay_context* ctx, long entityId);
void VRD_SetLog(VRD_replay_context* ctx, long entityId, const char* log, const char* category, enum VRD_Color color);
void VRD_SetPosition(VRD_replay_context* ctx, long entityId, VRD_Point* pos);
void VRD_SetTransform(VRD_replay_context* ctx, long entityId, VRD_Transform* xform);
void VRD_SetDynamicParamString(VRD_replay_context* ctx, long entityId, const char* key, const char* val);
void VRD_SetDynamicParamFloat(VRD_replay_context* ctx, long entityId, const char* key, float val);
void VRD_DrawSphere(VRD_replay_context* ctx, long entityId, const char* category, VRD_Point* pos, float radius, enum VRD_Color color);
void VRD_DrawBox(VRD_replay_context* ctx, long entityId, const char* category, VRD_Transform* xform, VRD_Point* dimensions, enum VRD_Color color);
void VRD_DrawCapsule(VRD_replay_context* ctx, long entityId, const char* category, VRD_Point* p1, VRD_Point* p2, float radius, enum VRD_Color color);
void VRD_DrawMesh(VRD_replay_context* ctx, long entityId, const char* category, VRD_Point* verts, int vertCount, enum VRD_Color color);
void VRD_DrawLine(VRD_replay_context* ctx, long entityId, const char* category, VRD_Point* p1, VRD_Point* p2, enum VRD_Color color);
void VRD_DrawCircle(VRD_replay_context* ctx, long entityId, const char* category, VRD_Point* position, VRD_Point* up, float radius, enum VRD_Color color);
void VRD_StepFrame(VRD_replay_context* ctx, float totalTime);

#ifdef __cplusplus
}
#endif

#endif //REPLAYCAPTURE_H