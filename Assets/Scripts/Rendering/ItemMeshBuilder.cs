#nullable enable
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

using MinecraftClient.Mapping;
using MinecraftClient.Resource;
using MinecraftClient.Inventory;

namespace MinecraftClient.Rendering
{
    public class ItemMeshBuilder
    {
        public static (Mesh mesh, Material material, Dictionary<DisplayPosition, float3x3> transforms)? BuildItem(ItemStack? itemStack)
        {
            if (itemStack is null) return null;

            var packManager = ResourcePackManager.Instance;
            var itemId = itemStack.ItemType.ItemId;

            var itemNumId = ItemPalette.INSTANCE.ToNumId(itemId);
            packManager.ItemModelTable.TryGetValue(itemNumId, out ItemModel? itemModel);

            if (itemModel is null) return null;

            // Make and set mesh...
            var visualBuffer = new VertexBuffer();

            int fluidVertexCount = visualBuffer.vert.Length;
            int fluidTriIdxCount = (fluidVertexCount / 2) * 3;

            float3[] colors;

            var tintFunc = ItemPalette.INSTANCE.GetTintRule(itemId);
            if (tintFunc is null)
                colors = new float3[]{ new(1F, 0F, 0F), new(0F, 0F, 1F), new(0F, 1F, 0F) };
            else
                colors = tintFunc.Invoke(itemStack);

            // TODO Get and build the right geometry (base or override)
            var itemGeometry = itemModel.Geometry;
            itemGeometry.Build(ref visualBuffer, float3.zero, colors);

            int vertexCount = visualBuffer.vert.Length;
            int triIdxCount = (vertexCount / 2) * 3;

            var meshDataArr = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArr[0];

            var vertAttrs = new NativeArray<VertexAttributeDescriptor>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            vertAttrs[0] = new(VertexAttribute.Position,  dimension: 3, stream: 0);
            vertAttrs[1] = new(VertexAttribute.TexCoord0, dimension: 3, stream: 1);
            vertAttrs[2] = new(VertexAttribute.Color,     dimension: 3, stream: 2);

            // Set mesh params
            meshData.SetVertexBufferParams(vertexCount, vertAttrs);
            vertAttrs.Dispose();

            meshData.SetIndexBufferParams(triIdxCount, IndexFormat.UInt32);

            // Set vertex data
            // Positions
            var positions = meshData.GetVertexData<float3>(0);
            positions.CopyFrom(visualBuffer.vert);
            // Tex Coordinates
            var texCoords = meshData.GetVertexData<float3>(1);
            texCoords.CopyFrom(visualBuffer.txuv);
            // Vertex colors
            var vertColors = meshData.GetVertexData<float3>(2);
            vertColors.CopyFrom(visualBuffer.tint);

            // Set face data
            var triIndices = meshData.GetIndexData<uint>();
            uint vi = 0; int ti = 0;
            for (;vi < vertexCount;vi += 4U, ti += 6)
            {
                triIndices[ti]     = vi;
                triIndices[ti + 1] = vi + 3U;
                triIndices[ti + 2] = vi + 2U;
                triIndices[ti + 3] = vi;
                triIndices[ti + 4] = vi + 1U;
                triIndices[ti + 5] = vi + 3U;
            }

            var bounds = new Bounds(new Vector3(0.5F, 0.5F, 0.5F), new Vector3(1F, 1F, 1F));

            meshData.subMeshCount = 1;
            meshData.SetSubMesh(0, new SubMeshDescriptor(0, triIdxCount)
            {
                bounds = bounds,
                vertexCount = vertexCount
            }, MeshUpdateFlags.DontRecalculateBounds);

            var mesh = new Mesh
            {
                bounds = bounds,
                name = "Proc Mesh"
            };

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArr, mesh);

            // Recalculate mesh normals
            mesh.RecalculateNormals();

            var material = CornApp.CurrentClient!.MaterialManager!.GetAtlasMaterial(itemModel.RenderType);;

            return (mesh, material, itemGeometry.DisplayTransforms);
        }
    }
}