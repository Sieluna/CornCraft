using UnityEngine;

namespace MinecraftClient.Rendering
{
    public class PlaceboGeometry
    {
        public static Vector3[] GetUpVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(0 + blockZ, 1 + blockY, 1 + blockX), // 4 => 2
                new Vector3(1 + blockZ, 1 + blockY, 1 + blockX), // 5 => 3
                new Vector3(0 + blockZ, 1 + blockY, 0 + blockX), // 3 => 1
                new Vector3(1 + blockZ, 1 + blockY, 0 + blockX), // 2 => 0
            };
        }

        public static Vector3[] GetDownVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(0 + blockZ, 0 + blockY, 0 + blockX), // 0 => 0
                new Vector3(1 + blockZ, 0 + blockY, 0 + blockX), // 1 => 1
                new Vector3(0 + blockZ, 0 + blockY, 1 + blockX), // 7 => 3
                new Vector3(1 + blockZ, 0 + blockY, 1 + blockX), // 6 => 2
            };
        }

        public static Vector3[] GetNorthVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(0 + blockZ, 1 + blockY, 1 + blockX), // 4 => 2
                new Vector3(0 + blockZ, 1 + blockY, 0 + blockX), // 3 => 1
                new Vector3(0 + blockZ, 0 + blockY, 1 + blockX), // 7 => 3
                new Vector3(0 + blockZ, 0 + blockY, 0 + blockX), // 0 => 0
            };
        }

        public static Vector3[] GetSouthVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(1 + blockZ, 1 + blockY, 0 + blockX), // 2 => 1
                new Vector3(1 + blockZ, 1 + blockY, 1 + blockX), // 5 => 2
                new Vector3(1 + blockZ, 0 + blockY, 0 + blockX), // 1 => 0
                new Vector3(1 + blockZ, 0 + blockY, 1 + blockX), // 6 => 3
            };
        }

        public static Vector3[] GetWestVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(0 + blockZ, 1 + blockY, 0 + blockX), // 3 => 3
                new Vector3(1 + blockZ, 1 + blockY, 0 + blockX), // 2 => 2
                new Vector3(0 + blockZ, 0 + blockY, 0 + blockX), // 0 => 0
                new Vector3(1 + blockZ, 0 + blockY, 0 + blockX), // 1 => 1
            };
        }

        public static Vector3[] GetEastVertices(int blockX, int blockY, int blockZ)
        {
            return new Vector3[]
            {
                new Vector3(1 + blockZ, 1 + blockY, 1 + blockX), // 5 => 1
                new Vector3(0 + blockZ, 1 + blockY, 1 + blockX), // 4 => 0
                new Vector3(1 + blockZ, 0 + blockY, 1 + blockX), // 6 => 2
                new Vector3(0 + blockZ, 0 + blockY, 1 + blockX), // 7 => 3
            };
        }

        public static int[] GetQuad(int offset)
        {
            return new int[]
            {
                0 + offset, 3 + offset, 2 + offset, // MC: +X <=> Unity: +Z
                0 + offset, 1 + offset, 3 + offset
            };
        }

        private const int TexturesInALine = 32;
        private const float One = 1.0F / TexturesInALine; // Size of a single block texture

        public static Vector2[] GetUVs(RenderType type)
        {
            int offset = type switch
            {
                RenderType.SOLID         => 0,
                RenderType.CUTOUT        => 1,
                RenderType.CUTOUT_MIPPED => 2,
                RenderType.TRANSLUCENT   => 3,

                _                        => 0
            };

            float blockU = (offset % TexturesInALine) / (float)TexturesInALine;
            float blockV = (offset / TexturesInALine) / (float)TexturesInALine;
            Vector2 o = new Vector2(blockU, blockV);

            return new Vector2[]{ new Vector2(0F, 0F) + o, new Vector2(One, 0F) + o, new Vector2(0F, One) + o, new Vector2(One, One) + o };

        }

        public static void Build(ref MeshBuffer buffer, RenderType type, bool uv, int x, int y, int z, int cullFlags)
        {
            if ((cullFlags & (1 << 0)) != 0) // Up
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetUpVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
            if ((cullFlags & (1 << 1)) != 0) // Down
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetDownVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
            if ((cullFlags & (1 << 2)) != 0) // South
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetSouthVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
            if ((cullFlags & (1 << 3)) != 0) // North
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetNorthVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
            if ((cullFlags & (1 << 4)) != 0) // East
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetEastVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
            if ((cullFlags & (1 << 5)) != 0) // West
            {
                buffer.vert = ArrayUtil.GetConcated(buffer.vert, PlaceboGeometry.GetWestVertices(x, y, z));
                buffer.face = ArrayUtil.GetConcated(buffer.face, GetQuad(buffer.offset));
                if (uv) buffer.uv = ArrayUtil.GetConcated(buffer.uv, GetUVs(type));
                buffer.offset += 4;
            }
        }
    
    }

}