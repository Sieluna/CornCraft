#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using MinecraftClient.Mapping;

namespace MinecraftClient.Rendering
{
    public class EntityManager : MonoBehaviour
    {
        private static EntityManager? instance;
        public static EntityManager Instance
        {
            get {
                if (instance is null)
                {
                    instance = Component.FindObjectOfType<EntityManager>();
                }
                
                return instance;
            }
        }

        private GameObject? entityPrefab;

        private static Dictionary<int, EntityRender> entities = new();

        public string GetDebugInfo()
        {
            return $"Ent: {entities.Count}";
        }

        public void AddEntity(Entity entity)
        {
            var entityObj    = GameObject.Instantiate(entityPrefab);
            var entityRender = entityObj!.GetComponent<EntityRender>();
            entityRender.Entity = entity;
            entities.Add(entity.ID, entityRender);

            entityObj.name = $"{entity.ID} {entity.Type}";
            entityObj.transform.parent = transform;

        }

        public void RemoveEntities(int[] entityIds)
        {
            foreach (var id in entityIds)
            {
                if (entities.ContainsKey(id))
                {
                    entities[id].Unload();
                    entities.Remove(id);
                }
            }
        }

        public void TeleportEntity(int entityId, Location location)
        {
            if (entities.ContainsKey(entityId))
            {
                entities[entityId].TeleportTo(CoordConvert.MC2Unity(location));
            }
        }

        public void UnloadEntities()
        {
            entities.Clear();

            // Reset instance
            instance = null;
        }

        public void ReloadEntities()
        {
            var ids = entities.Keys.ToArray();

            foreach (var id in ids)
                entities[id].Unload();

            entities.Clear();

        }

        void Start()
        {
            entityPrefab = Resources.Load<GameObject>("Prefabs/Entity");
            
        }

    }
}