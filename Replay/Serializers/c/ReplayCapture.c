// SPDX-License-Identifier: MIT
#include "ReplayCapture.h"
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#ifndef VRD_ZLIB_COMPRESSION_LEVEL
#define VRD_ZLIB_COMPRESSION_LEVEL 1
#endif

#ifndef VRD_ZLIB_COMPRESSION_BUFFER_SIZE
#define VRD_ZLIB_COMPRESSION_BUFFER_SIZE (128<<10)
#endif

#ifdef VRD_USE_ZLIB
#include "ThirdParty/zlib/zlib-1.2.5/Inc/zlib.h"
#if defined(MSDOS) || defined(OS2) || defined(WIN32) || defined(__CYGWIN__)
#  include <fcntl.h>
#  include <io.h>
#  define SET_BINARY_MODE(file) setmode(fileno(file), O_BINARY)
#else
#  define SET_BINARY_MODE(file)
#endif
#endif //VRD_USE_ZLIB

struct VRD_EntityMapItem { entityKeyType address; };

struct VRD_replay_context
{
	int status;
	FILE* fp;
	int frame;
	VRD_EntityMapItem* entity_map;
	int entity_map_capacity;
	int entity_map_count;

#ifdef VRD_USE_ZLIB
	z_stream z_strm;
	void* z_compression_buffer;
#endif

};

int VRD_Internal_EntityMap(VRD_replay_context* ctx, entityKeyType entityAddr);
void VRD_Internal_WriteReplayHeader(VRD_replay_context* ctx);
void VRD_Internal_WriteEntityDef(VRD_replay_context* ctx, int entityId, int frame, const char* name, const char* path, const char* type_name, const char* category_name, VRD_Transform* xform, VRD_StringDictPair* staticParams, int staticParamsCount);
void VRD_Internal_WriteEntityUndef(VRD_replay_context* ctx, int entityId, int frame);
void VRD_Internal_WriteEntityPosition(VRD_replay_context* ctx, int entityId, int frame, VRD_Point* pos);
void VRD_Internal_WriteEntityTransform(VRD_replay_context* ctx, int entityId, int frame, VRD_Transform* xform);
void VRD_Internal_WriteEntityLog(VRD_replay_context* ctx, int entityId, int frame, const char* log, const char* category, enum VRD_Color color);
void VRD_Internal_SetDynamicParamString(VRD_replay_context* ctx, int entityId, int frame, const char* key, const char* val);
void VRD_Internal_SetDynamicParamFloat(VRD_replay_context* ctx, int entityId, int frame, const char* key, float val);
void VRD_Internal_DrawSphere(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* pos, float radius, enum VRD_Color color);
void VRD_Internal_DrawBox(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Transform* xform, VRD_Point* dimensions, enum VRD_Color color);
void VRD_Internal_DrawCapsule(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* p1, VRD_Point* p2, float radius, enum VRD_Color color);
void VRD_Internal_DrawMesh(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* verts, int vertCount, enum VRD_Color color);
void VRD_Internal_DrawLine(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* p1, VRD_Point* p2, enum VRD_Color color);
void VRD_Internal_DrawCircle(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* position, VRD_Point* up, float radius, enum VRD_Color color);
void VRD_Internal_WriteFrameStep(VRD_replay_context* ctx, float totalTime);

VRD_replay_context* VRD_CreateContext(const char* filename, int compressed)
{
	FILE* fp;
	if (fopen_s(&fp, filename, "wb") != 0) return 0;

	VRD_replay_context* ctx = (VRD_replay_context*)malloc(sizeof(VRD_replay_context));
	if (ctx)
	{
		memset(ctx, 0, sizeof(VRD_replay_context));
		ctx->status = 1;
		ctx->fp = fp;

#ifdef VRD_USE_ZLIB
		ctx->z_strm.zalloc = 0;
		ctx->z_strm.zfree = 0;
		ctx->z_strm.opaque = 0;
		deflateInit2(&ctx->z_strm, VRD_ZLIB_COMPRESSION_LEVEL, Z_DEFLATED, -15, 9, Z_DEFAULT_STRATEGY); // -15 for raw stream, without zlib header
		ctx->z_compression_buffer = malloc(VRD_ZLIB_COMPRESSION_BUFFER_SIZE);
#endif

		VRD_Internal_WriteReplayHeader(ctx);
	}
	return ctx;
}

void VRD_ReleaseContext(VRD_replay_context* ctx)
{
	if (ctx != 0)
	{
#ifdef VRD_USE_ZLIB
		ctx->z_strm.avail_out = VRD_ZLIB_COMPRESSION_BUFFER_SIZE;
		ctx->z_strm.next_out = ctx->z_compression_buffer;
		if (deflate(&ctx->z_strm, Z_FINISH) != Z_STREAM_ERROR)
		{
			int writeCount = VRD_ZLIB_COMPRESSION_BUFFER_SIZE - ctx->z_strm.avail_out;
			fwrite(ctx->z_compression_buffer, writeCount, 1, ctx->fp);
		}

		deflateEnd(&ctx->z_strm);
		free(ctx->z_compression_buffer);
#endif
		fclose(ctx->fp);
		free(ctx->entity_map);
		memset(ctx, 0, sizeof(VRD_replay_context));
		free(ctx);
	}
}

void VRD_RegisterEntity(VRD_replay_context* ctx, entityKeyType entityId, const char* name, const char* path, const char* type_name, const char* category_name, VRD_Transform* transform, VRD_StringDictPair* staticParams, int staticParamsCount)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_WriteEntityDef(ctx, id, ctx->frame, name, path, type_name, category_name, transform, staticParams, staticParamsCount);
}

void VRD_UnRegisterEntity(VRD_replay_context* ctx, entityKeyType entityId)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_WriteEntityUndef(ctx, id, ctx->frame);
}

void VRD_SetLog(VRD_replay_context* ctx, entityKeyType entityId, const char* log, const char* category, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_WriteEntityLog(ctx, id, ctx->frame, log, category, color);
}

void VRD_SetPosition(VRD_replay_context* ctx, entityKeyType entityId, VRD_Point* pos)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_WriteEntityPosition(ctx, id, ctx->frame, pos);
}

void VRD_SetTransform(VRD_replay_context* ctx, entityKeyType entityId, VRD_Transform* xform)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_WriteEntityTransform(ctx, id, ctx->frame, xform);
}

void VRD_SetDynamicParamString(VRD_replay_context* ctx, entityKeyType entityId, const char* key, const char* val)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_SetDynamicParamString(ctx, id, ctx->frame, key, val);
}

void VRD_SetDynamicParamFloat(VRD_replay_context* ctx, entityKeyType entityId, const char* key, float val)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_SetDynamicParamFloat(ctx, id, ctx->frame, key, val);
}

void VRD_DrawSphere(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Point* pos, float radius, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawSphere(ctx, id, ctx->frame, category, pos, radius, color);
}

void VRD_DrawBox(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Transform* xform, VRD_Point* dimensions, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawBox(ctx, id, ctx->frame, category, xform, dimensions, color);
}

void VRD_DrawCapsule(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Point* p1, VRD_Point* p2, float radius, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawCapsule(ctx, id, ctx->frame, category, p1, p2, radius, color);
}

void VRD_DrawMesh(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Point* verts, int vertCount, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawMesh(ctx, id, ctx->frame, category, verts, vertCount, color);
}

void VRD_DrawLine(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Point* p1, VRD_Point* p2, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawLine(ctx, id, ctx->frame, category, p1, p2, color);
}

void VRD_DrawCircle(VRD_replay_context* ctx, entityKeyType entityId, const char* category, VRD_Point* position, VRD_Point* up, float radius, enum VRD_Color color)
{
	if (ctx == 0 || ctx->status == 0) return;
	int id = VRD_Internal_EntityMap(ctx, entityId);
	VRD_Internal_DrawCircle(ctx, id, ctx->frame, category, position, up, radius, color);
}

void VRD_StepFrame(VRD_replay_context* ctx, float totalTime)
{
	if (ctx == 0 || ctx->status == 0) return;
	VRD_Internal_WriteFrameStep(ctx, totalTime);
	ctx->frame += 1;
}

/// 
/// Internal
///

enum VRD_BlockType
{
	None = 0,
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
};


int VRD_Internal_EntityMap(VRD_replay_context* ctx, entityKeyType entityAddr)
{
	if (ctx == 0 || ctx->status == 0) return -1;

	if (sizeof(entityKeyType) == sizeof(int)) return (entityAddr + 1); // No need to map if key is 32 bits

	// Note: This could be replaced with a hash-map
	// *BUT* if this becomes a bottleneck, it means we have way too many entities

	// find id in map
	if (ctx->entity_map_count > 0)
	{
		VRD_EntityMapItem* it = ctx->entity_map;
		int nb = ctx->entity_map_count;
		for (int i = 0; i < nb; ++i)
		{
			if (it->address == entityAddr) return (i + 1);
			++it;
		}
	}
	// Not found

	// Make sure there is room in the map
	const int entityMapGrowthSize = 16 << 10;
	if (ctx->entity_map == 0)
	{
		ctx->entity_map = static_cast<VRD_EntityMapItem*>(malloc(entityMapGrowthSize * sizeof(VRD_EntityMapItem)));
		ctx->entity_map_capacity = entityMapGrowthSize;
		ctx->entity_map_count = 0;
	}
	else if (ctx->entity_map_count >= ctx->entity_map_capacity)
	{
		VRD_EntityMapItem* orig = ctx->entity_map;
		ctx->entity_map = static_cast<VRD_EntityMapItem*>(realloc(orig, (ctx->entity_map_capacity + entityMapGrowthSize) * sizeof(VRD_EntityMapItem)));
		// TODO: handle realloc fail here.
		ctx->entity_map_capacity += entityMapGrowthSize;
	}

	// insert in map
	VRD_EntityMapItem addr = { entityAddr };
	if (ctx->entity_map != 0)
	{
		int index = ctx->entity_map_count;
		ctx->entity_map[index] = addr;
		ctx->entity_map_count += 1;
		return (index + 1);
	}

	return -1;
}

void VRD_Internal_Write(VRD_replay_context* ctx, void* buffer, int buffer_len)
{
#ifdef VRD_USE_ZLIB
	ctx->z_strm.avail_in = buffer_len;
	ctx->z_strm.next_in = buffer;
	ctx->z_strm.avail_out = VRD_ZLIB_COMPRESSION_BUFFER_SIZE;
	ctx->z_strm.next_out = ctx->z_compression_buffer;
	if (deflate(&ctx->z_strm, Z_NO_FLUSH) != Z_STREAM_ERROR)
	{
		int writeCount = VRD_ZLIB_COMPRESSION_BUFFER_SIZE - ctx->z_strm.avail_out;
		fwrite(ctx->z_compression_buffer, writeCount, 1, ctx->fp);
	}
#else
	fwrite(buffer, buffer_len, 1, ctx->fp);
#endif
}

void VRD_Internal_WriteChar(VRD_replay_context* ctx, char value)
{
	VRD_Internal_Write(ctx, &value, 1);
}

void VRD_Internal_WriteInt(VRD_replay_context* ctx, int value)
{
	VRD_Internal_Write(ctx, &value, 4);
}

void VRD_Internal_WriteFloat(VRD_replay_context* ctx, float value)
{
	VRD_Internal_Write(ctx, &value, 4);
}

void VRD_Internal_Write7BitEncodedInt(VRD_replay_context* ctx, int value)
{
	int num = (int)value;
	while (num >= 0x80)
	{
		VRD_Internal_WriteChar(ctx, (char)(num | 0x80));
		num = num >> 7;
	}
	VRD_Internal_WriteChar(ctx, (char)num);
}

void VRD_Internal_WriteColor(VRD_replay_context* ctx, enum VRD_Color color)
{
	VRD_Internal_Write7BitEncodedInt(ctx, color);
}

void VRD_Internal_WriteString(VRD_replay_context* ctx, const char* s)
{
	int len = 0;
	if (s) { len = strlen(s); }
	VRD_Internal_Write7BitEncodedInt(ctx, len);
	if (len > 0)
	{
		VRD_Internal_Write(ctx, (void*)s, len);
	}
}

static VRD_Point VRD_PointZero = { 0,0,0 };
static VRD_Transform VRD_Identity = { {0,0,0},{0,0,0,1} };

void VRD_Internal_WritePoint(VRD_replay_context* ctx, VRD_Point* point)
{
	if (point == 0)
	{
		point = &VRD_PointZero;
	}

	VRD_Internal_WriteFloat(ctx, point->x);
	VRD_Internal_WriteFloat(ctx, point->y);
	VRD_Internal_WriteFloat(ctx, point->z);
}

void VRD_Internal_WriteTransform(VRD_replay_context* ctx, VRD_Transform* xform)
{
	if (xform == 0)
	{
		xform = &VRD_Identity;
	}

	VRD_Internal_WriteFloat(ctx, xform->translation.x);
	VRD_Internal_WriteFloat(ctx, xform->translation.y);
	VRD_Internal_WriteFloat(ctx, xform->translation.z);
	VRD_Internal_WriteFloat(ctx, xform->rotation.x);
	VRD_Internal_WriteFloat(ctx, xform->rotation.y);
	VRD_Internal_WriteFloat(ctx, xform->rotation.z);
	VRD_Internal_WriteFloat(ctx, xform->rotation.w);
}

void VRD_Internal_WriteReplayHeader(VRD_replay_context* ctx)
{
#ifdef VRD_USE_ZLIB
	VRD_Internal_Write7BitEncodedInt(ctx, ReplayHeader);
#else
	VRD_Internal_WriteInt(ctx, ReplayHeader);
#endif
}

void VRD_Internal_WriteEntityHeader(VRD_replay_context* ctx, enum VRD_BlockType blockType, int entityId, int frame)
{
	VRD_Internal_Write7BitEncodedInt(ctx, blockType);
	VRD_Internal_Write7BitEncodedInt(ctx, frame);
	VRD_Internal_Write7BitEncodedInt(ctx, entityId);
}

void VRD_Internal_WriteFrameStep(VRD_replay_context* ctx, float totalTime)
{
	VRD_Internal_Write7BitEncodedInt(ctx, FrameStep);
	VRD_Internal_WriteFloat(ctx, totalTime);
}

void VRD_Internal_WriteEntityDef(VRD_replay_context* ctx, int entityId, int frame, const char* name, const char* path, const char* type_name, const char* category_name, VRD_Transform* xform, VRD_StringDictPair* staticParams, int staticParamsCount)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityDef, entityId, frame);
	VRD_Internal_Write7BitEncodedInt(ctx, entityId);

	VRD_Internal_WriteString(ctx, name);
	VRD_Internal_WriteString(ctx, path);
	VRD_Internal_WriteString(ctx, type_name);
	VRD_Internal_WriteString(ctx, category_name);
	VRD_Internal_WriteTransform(ctx, xform);
	VRD_Internal_Write7BitEncodedInt(ctx, staticParamsCount);
	for (int i = 0; i < staticParamsCount; ++i)
	{
		VRD_Internal_WriteString(ctx, staticParams[i].key);
		VRD_Internal_WriteString(ctx, staticParams[i].value);
	}
	VRD_Internal_Write7BitEncodedInt(ctx, frame);

}

void VRD_Internal_WriteEntityUndef(VRD_replay_context* ctx, int entityId, int frame)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityUndef, entityId, frame);
}

void VRD_Internal_WriteEntityLog(VRD_replay_context* ctx, int entityId, int frame, const char* log, const char* category, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityLog, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WriteString(ctx, log);
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_WriteEntityPosition(VRD_replay_context* ctx, int entityId, int frame, VRD_Point* pos)
{
	VRD_Internal_WriteEntityHeader(ctx, EntitySetPos, entityId, frame);
	VRD_Internal_WritePoint(ctx, pos);
}

void VRD_Internal_WriteEntityTransform(VRD_replay_context* ctx, int entityId, int frame, VRD_Transform* xform)
{
	VRD_Internal_WriteEntityHeader(ctx, EntitySetTransform, entityId, frame);
	VRD_Internal_WriteTransform(ctx, xform);
}

void VRD_Internal_SetDynamicParamString(VRD_replay_context* ctx, int entityId, int frame, const char* key, const char* val)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityParameter, entityId, frame);
	VRD_Internal_WriteString(ctx, key);
	VRD_Internal_WriteString(ctx, val);
}

void VRD_Internal_SetDynamicParamFloat(VRD_replay_context* ctx, int entityId, int frame, const char* key, float val)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityValue, entityId, frame);
	VRD_Internal_WriteString(ctx, key);
	VRD_Internal_WriteFloat(ctx, val);
}

void VRD_Internal_DrawSphere(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* pos, float radius, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntitySphere, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WritePoint(ctx, pos);
	VRD_Internal_WriteFloat(ctx, radius);
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_DrawBox(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Transform* xform, VRD_Point* dimensions, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityValue, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WriteTransform(ctx, xform);
	VRD_Internal_WritePoint(ctx, dimensions);
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_DrawCapsule(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* p1, VRD_Point* p2, float radius, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityCapsule, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WritePoint(ctx, p1);
	VRD_Internal_WritePoint(ctx, p2);
	VRD_Internal_WriteFloat(ctx, radius);
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_DrawMesh(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* verts, int vertCount, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityMesh, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WriteInt(ctx, vertCount);
	for (int i = 0; i < vertCount; ++i)
	{
		VRD_Internal_WritePoint(ctx, &verts[i]);
	}
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_DrawLine(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* p1, VRD_Point* p2, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityLine, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WritePoint(ctx, p1);
	VRD_Internal_WritePoint(ctx, p2);
	VRD_Internal_WriteColor(ctx, color);
}

void VRD_Internal_DrawCircle(VRD_replay_context* ctx, int entityId, int frame, const char* category, VRD_Point* position, VRD_Point* up, float radius, enum VRD_Color color)
{
	VRD_Internal_WriteEntityHeader(ctx, EntityCircle, entityId, frame);
	VRD_Internal_WriteString(ctx, category);
	VRD_Internal_WritePoint(ctx, position);
	VRD_Internal_WritePoint(ctx, up);
	VRD_Internal_WriteFloat(ctx, radius);
	VRD_Internal_WriteColor(ctx, color);
}