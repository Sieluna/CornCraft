#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.Mathematics;

using CraftSharp.Molang.Runtime;
using CraftSharp.Molang.Runtime.Value;
using CraftSharp.Molang.Utils;
using CraftSharp.Resource;
using CraftSharp.Resource.BedrockEntity;

namespace CraftSharp.Rendering
{
    public class BedrockModelEntityRender : EntityRender
    {
        private EntityRenderDefinition? entityDefinition = null;

        private readonly Dictionary<string, GameObject> boneObjects = new();

        public string[] MaterialNames = { };
        private readonly Dictionary<string, string> materials = new();
        private Material? currentMaterial = null;

        public string[] TextureNames = { };
        private readonly Dictionary<string, Texture2D> textures = new();

        private EntityGeometry? geometry = null;

        public string[] AnimationNames = { };
        private EntityAnimation?[] animations = { };
        private EntityAnimation? currentAnimation = null;

        private readonly MoScope scope = new(new MoLangRuntime());
        private readonly MoLangEnvironment env = new();

        private string GetImagePathFromFileName(string name)
        {
            // Image could be either tga or png
            if (File.Exists($"{name}.png"))
            {
                return $"{name}.png";
            }
            else if (File.Exists($"{name}.tga"))
            {
                return $"{name}.tga";
            }

            // Nothing found
            return name;
        }

        public void SetDefinitionData(EntityRenderDefinition def)
        {
            entityDefinition = def;
        }

        public void BuildEntityModel(BedrockEntityResourceManager entityResManager, EntityMaterialManager matManager)
        {
            if (entityDefinition is null)
            {
                Debug.LogError("Entity definition not assigned!");
                return;
            }

            if (entityDefinition.GeometryNames.Count == 0)
            {
                Debug.LogWarning($"Entity definition has no geometry!");
                return;
            }

            var geometryName = entityDefinition.GeometryNames.First().Value;
            gameObject.name += $" ({geometryName})";

            if (!entityResManager.EntityGeometries.ContainsKey(geometryName))
            {
                // TODO: Debug.LogWarning($"Entity geometry [{geometryName}] not loaded!");
                return;
            }

            geometry = entityResManager.EntityGeometries[geometryName];

            foreach (var tex in entityDefinition.TexturePaths)
            {
                var fileName = GetImagePathFromFileName(tex.Value);
                // Load texture from file
                Texture2D texture;
                var imageBytes = File.ReadAllBytes(fileName);
                if (fileName.EndsWith(".tga")) // Read as tga image
                {
                    texture = TGALoader.TextureFromTGA(imageBytes);
                }
                else // Read as png image
                {
                    texture = new Texture2D(2, 2);
                    texture.LoadImage(imageBytes);
                }

                texture.filterMode = FilterMode.Point;
                //Debug.Log($"Loaded texture from {fileName} ({texture.width}x{texture.height})");

                if (geometry.TextureWidth != texture.width && geometry.TextureHeight != texture.height)
                {
                    if (geometry.TextureWidth == 0 && geometry.TextureHeight == 0) // Not specified, just use the size we have
                    {
                        geometry.TextureWidth = texture.width;
                        geometry.TextureHeight = texture.height;
                    }
                    else // The sizes doesn't match
                    {
                        //Debug.LogWarning($"Specified texture size({geometry.TextureWidth}x{geometry.TextureHeight}) doesn't match image file {tex.Value} ({texture.width}x{texture.height})!");
                    }
                }
            
                textures.Add(tex.Key, texture);
            }
            TextureNames = textures.Select(x => x.Key).ToArray();
            SetTexture(0);

            var matId = entityDefinition.MaterialIdentifiers.First().Value;
            var renderType = entityResManager.MaterialRenderTypes.GetValueOrDefault(matId);
            var materialTemplate = matManager.GetBedrockEntityMaterialTemplate(renderType);
            // Make a copy of the material
            currentMaterial = new Material(materialTemplate) { mainTexture = textures.First().Value };

            // Build mesh for each bone
            foreach (var bone in geometry.Bones.Values)
            {
                var boneObj = new GameObject($"Bone [{bone.Name}]");
                boneObj.transform.SetParent(transform, false);

                //var boneMeshObj = new GameObject($"Mesh [{bone.Name}]");
                //boneMeshObj.transform.SetParent(boneObj.transform, false);
                var boneMeshFilter = boneObj.AddComponent<MeshFilter>();
                var boneMeshRenderer = boneObj.AddComponent<MeshRenderer>();

                var visualBuffer = new EntityVertexBuffer();

                for (int i = 0;i < bone.Cubes.Length;i++)
                {
                    EntityCubeGeometry.Build(ref visualBuffer, geometry.TextureWidth, geometry.TextureHeight,
                            bone.MirrorUV, bone.Pivot, bone.Cubes[i]);
                }

                boneMeshFilter!.sharedMesh = EntityVertexBufferBuilder.BuildMesh(visualBuffer);
                boneMeshRenderer!.sharedMaterial = currentMaterial;

                boneObjects.Add(bone.Name, boneObj);
            }
            // Setup initial bone pose
            foreach (var bone in geometry.Bones.Values)
            {
                var boneTransform = boneObjects[bone.Name].transform;

                if (bone.ParentName is not null) // Set parent transform
                {
                    if (boneObjects.ContainsKey(bone.ParentName))
                    {
                        boneTransform.SetParent(boneObjects[bone.ParentName].transform, false);
                        boneTransform.localPosition = (bone.Pivot - geometry.Bones[bone.ParentName].Pivot) / 16F;
                        boneTransform.localRotation = Rotations.RotationFromEularsXYZ(bone.Rotation);
                    }
                    else
                    {
                        Debug.LogWarning($"In {geometryName}: parent bone {bone.ParentName} not found!");
                    }
                }
                else // Root bone
                {
                    boneTransform.localPosition = bone.Pivot / 16F;
                    boneTransform.localRotation = Rotations.RotationFromEularsXYZ(bone.Rotation);
                }
            }
        
            // Prepare animations
            AnimationNames = entityDefinition.AnimationNames.Select(x => $"{x.Key} ({x.Value})").ToArray();
            animations = entityDefinition.AnimationNames.Select(x => 
                    {
                        EntityAnimation? anim;

                        if (entityResManager.EntityAnimations.ContainsKey(x.Value))
                        {
                            anim = entityResManager.EntityAnimations[x.Value];
                        }
                        else
                        {
                            anim = null;
                            // TODO: Debug.LogWarning($"Animation [{x.Value}] not loaded!");
                        }

                        return anim; 
                    }).ToArray();
        }

        public void SetTexture(int index)
        {
            if (index >= 0 && index < TextureNames.Length)
            {
                if (currentMaterial != null)
                {
                    currentMaterial.mainTexture = textures[TextureNames[index]];
                }
            }
            else
            {
                throw new System.Exception($"Invalid texture index: {index}");
            }
        }

        public EntityAnimation? SetAnimation(int index, float initialTime)
        {
            if (index >= 0 && index < animations.Length)
            {
                currentAnimation = animations[index];
                UpdateAnimation(initialTime);

                return currentAnimation;
            }
            else
            {
                throw new System.Exception($"Invalid animation index: {index}");
            }
        }

        public override void UpdateAnimation(float time)
        {
            if (currentAnimation != null && geometry != null) // An animation file is present
            {
                foreach (var boneAnim in currentAnimation.BoneAnimations)
                {
                    if (boneObjects.ContainsKey(boneAnim.Key))
                    {
                        var (trans, scale, rot) = boneAnim.Value.Evaluate(time, scope, env);
                        UpdateBone(boneAnim.Key, trans, scale, rot);
                    }
                    else
                    {
                        Debug.Log($"Trying to update bone [{boneAnim.Key}] which is not present!");
                    }
                }
            }
        }

        public void UpdateMolangValue(MoPath varName, IMoValue value)
        {
            env.SetValue(varName, value);
        }

        private void UpdateBone(string boneName, float3? trans, float3? scale, float3? rot)
        {
            var boneTransform = boneObjects[boneName].transform;
            var bone = geometry!.Bones[boneName];

            if (trans is not null)
            {
                var converted = trans.Value.zyx;
                converted.z = -converted.z;

                float3 offset;

                if (bone.ParentName is not null)
                    offset = (converted + bone.Pivot - geometry.Bones[bone.ParentName].Pivot) / 16F;
                else
                    offset = (converted + bone.Pivot) / 16F;

                boneTransform.localPosition = offset;
            }
            
            if (rot is not null)
            {
                var converted = rot.Value.zyx;
                converted.x = -converted.x;
                boneTransform.localRotation = Rotations.RotationFromEularsXYZ(converted);
            }
        }
    }
}