using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BulletRicochet
{
    // Duckov 모드 엔트리
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private Harmony _patcher;
        public const string HARMONY_ID = "Souls.BulletVsBulletRicochet";

        private void OnEnable()
        {
            if (_patcher == null)
                _patcher = new Harmony(HARMONY_ID);

            _patcher.PatchAll();
            Debug.Log("[BulletRicochet] Harmony 패치 적용 완료");
        }

        private void OnDisable()
        {
            if (_patcher != null)
               

            Debug.Log("[BulletRicochet] Harmony 패치 해제");
        }
    }

    // ─────────────────────────────────────────────
    //  탄환 vs 탄환 도탄 로직 (타협 버전)
    // ─────────────────────────────────────────────
    [HarmonyPatch]
    public static class BulletVsBulletRicochetWorker
    {
        private static readonly List<Projectile> ActiveProjectiles = new List<Projectile>();

        // 판정 범위: 1m 안쪽이면 "충돌 후보"
        private const float HitRadius = 1.0f;
        private const float HitRadiusSqr = HitRadius * HitRadius;

        private static readonly FieldInfo VelocityField = AccessTools.Field(typeof(Projectile), "velocity");
        private static readonly FieldInfo DirectionField = AccessTools.Field(typeof(Projectile), "direction");

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Projectile), "UpdateMoveAndCheck")]
        public static void ProjectileUpdateMovePrefix(
            Projectile __instance,
            ref Vector3 ___velocity,
            ref Vector3 ___direction)
        {
            if (__instance == null)
                return;

            if (!ActiveProjectiles.Contains(__instance))
                ActiveProjectiles.Add(__instance);

            Vector3 selfPos = __instance.transform.position;
            int selfId = __instance.GetInstanceID();

            for (int i = ActiveProjectiles.Count - 1; i >= 0; i--)
            {
                Projectile other = ActiveProjectiles[i];

                if (other == null)
                {
                    ActiveProjectiles.RemoveAt(i);
                    continue;
                }

                if (other == __instance)
                    continue;

                // 한 쌍당 한쪽(더 낮은 ID)에서만 처리해서 중복 방지
                int otherId = other.GetInstanceID();
                if (selfId > otherId)
                    continue;

                Vector3 otherPos = other.transform.position;
                Vector3 delta = selfPos - otherPos;
                float distSqr = delta.sqrMagnitude;

                // 거리 필터
                if (distSqr > HitRadiusSqr || distSqr < 0.0001f)
                    continue;

                // ── 속도 읽기 ──
                Vector3 selfVel = ___velocity;
                Vector3 otherVel = Vector3.zero;

                if (VelocityField != null)
                {
                    object raw = VelocityField.GetValue(other);
                    if (raw is Vector3 v)
                        otherVel = v;
                }

                // ── 서로 가까워지는 중인지 확인 (접근 필터) ──
                // delta = self - other
                // relativeVel = selfVel - otherVel
                // dot(delta, relativeVel) < 0  → 서로 접근 중
                Vector3 relativeVel = selfVel - otherVel;
                float approach = Vector3.Dot(delta, relativeVel);
                if (approach >= 0.0f)
                {
                    // 멀어지는 중이면 안 튕김
                    continue;
                }

                // 충돌 면 법선
                Vector3 normal = delta.normalized;
                if (normal.sqrMagnitude < 1e-4f)
                    normal = Vector3.forward;

                // ─────────────────────
                //  실제 도탄 처리
                // ─────────────────────

                // 자기 탄환 반사
                Vector3 newSelfVel = Vector3.Reflect(selfVel, normal);
                ___velocity = newSelfVel;
                if (newSelfVel.sqrMagnitude > 0.0001f)
                    ___direction = newSelfVel.normalized;

                // 상대 탄환 반사
                if (VelocityField != null)
                {
                    Vector3 newOtherVel = Vector3.Reflect(otherVel, -normal);
                    VelocityField.SetValue(other, newOtherVel);

                    if (DirectionField != null)
                    {
                        Vector3 newOtherDir = newOtherVel.sqrMagnitude > 0.0001f
                            ? newOtherVel.normalized
                            : ___direction;
                        DirectionField.SetValue(other, newOtherDir);
                    }
                }

                Debug.Log("[BulletRicochet] 탄환-탄환 도탄 (접근필터+거리)");

                // 한 프레임에 한 번만 처리
                break;
            }
        }
    }
}
