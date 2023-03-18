#nullable enable
using System.Collections.Generic;
using UnityEngine;

using MinecraftClient.Rendering;

namespace MinecraftClient.Control
{
    public class MeleeWeapon : MonoBehaviour
    {
        private readonly List<Collider> slashHits = new();
        private bool slashActive = false;

        public TrailRenderer? slashTrail;

        public void StartSlash()
        {
            slashHits.Clear();
            slashActive = true;

            if (slashTrail is not null)
                slashTrail.emitting = true;
        }

        public List<AttackHitInfo> EndSlash()
        {
            slashActive = false;

            if (slashTrail is not null)
                slashTrail.emitting = false;
            
            List<AttackHitInfo> infos = new();

            foreach (var hit in slashHits)
            {
                EntityRender? entityRender;

                if (entityRender = hit.GetComponentInParent<EntityRender>())
                {
                    Debug.Log($"Slash hit {entityRender.gameObject.name}");
                    infos.Add(new(entityRender, hit));
                }
            }

            return infos;
        }

        void OnTriggerEnter(Collider hit)
        {
            if (!slashActive)
                return;
            
            if (!slashHits.Contains(hit))
                slashHits.Add(hit);
            
        }

    }
}