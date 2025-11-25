using System;
using HarmonyLib;
using UnityEngine;

namespace BulletRicochet
{
    public class ModEntry : MonoBehaviour
    {
        private Harmony harmony;

        void Awake()
        {
            // Harmony 인스턴스 생성
            harmony = new Harmony("com.example.bulletricochet");

            // 대상 클래스 찾기
            Type bulletType = AccessTools.TypeByName("TeamSoda.Duckov.Projectile") ?? AccessTools.TypeByName("TeamSoda.Duckov.Bullet");
            if (bulletType != null)
            {
                var targetMethod = AccessTools.Method(bulletType, "OnCollisionEnter", new Type[] { typeof(Collision) });
                if (targetMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(BulletCollisionPatch).GetMethod(nameof(BulletCollisionPatch.Prefix)));
                    harmony.Patch(targetMethod, prefix: prefix);
                }
            }

            // 충돌 레이어 설정 (예시: Bullet 레이어 번호)
            int bulletLayer = LayerMask.NameToLayer("Bullet");
            if (bulletLayer >= 0)
            {
                Physics.IgnoreLayerCollision(bulletLayer, bulletLayer, false);
            }
        }

        void OnDestroy()
        {
            harmony?.UnpatchSelf();
        }
    }

    public static class BulletCollisionPatch
    {
        public static bool Prefix(object __instance, Collision collision)
        {
            GameObject selfObj = (__instance as MonoBehaviour)?.gameObject;
            GameObject otherObj = collision?.gameObject;
            if (selfObj == null || otherObj == null) return true;

            Type bulletType = __instance.GetType();
            Component otherBullet = otherObj.GetComponent(bulletType);

            if (otherBullet != null)
            {
                int selfId = selfObj.GetInstanceID();
                int otherId = otherObj.GetInstanceID();

                if (selfId < otherId && collision.contactCount > 0)
                {
                    Rigidbody rbA = selfObj.GetComponent<Rigidbody>();
                    Rigidbody rbB = otherObj.GetComponent<Rigidbody>();
                    Vector3 normal = collision.GetContact(0).normal;

                    if (rbA != null) rbA.velocity = Vector3.Reflect(rbA.velocity, normal);
                    if (rbB != null) rbB.velocity = Vector3.Reflect(rbB.velocity, normal);
                }

                return false; // 기본 충돌 처리 방지
            }

            return true;
        }
    }
}
