using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BamStructure
{
    public class JobDriver_PlayDarts : JobDriver
    {
        private const int ThrowSpeed = 24;
        private const int ThrowInterval = 180;
        TargetIndex BoardIndex => TargetIndex.A;
        FleckDef dart;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(BoardIndex);
            Building board = job.GetTarget(BoardIndex).Thing as Building;

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            dart = GetDartFromIndex(Rand.Range(0,4)); //DefDatabase<FleckDef>.GetNamed("Fleck_Dart");
            Toil play = new Toil()
            {
                socialMode = RandomSocialMode.SuperActive,
                handlingFacing = true,
                defaultCompleteMode = ToilCompleteMode.Never
            };
            play.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(board);
            };
            play.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(ThrowInterval)) ThrowDartFleck(pawn, board);
                if (pawn.IsHashIntervalTick(60) && JoyUtility.JoyTickCheckEnd(pawn, 60, JoyTickFullJoyAction.EndJob, 1f, board)) { EndJobWith(JobCondition.Succeeded); return; }
            };
            yield return play;
        }

        private void ThrowDartFleck(Pawn thrower, Thing board)
        {
            if (thrower?.Map == null || board == null) return;
            Vector3 from = thrower.DrawPos;
            Vector3 to = board.DrawPos;
            Vector3 dir = to - from;
            dir.y = 0f;
            dir.Normalize();
            Vector3 left = new Vector3(-dir.z, 0, dir.x);

            Vector2 miss = new Vector2(Rand.Range(-0.1f, 0.1f), Rand.Range(-0.025f, 0f));// * thrower.GetStatValue(StatDefOf.MortarMissRadiusFactor);
            //bool centerHit = miss.sqrMagnitude <= 0.00001f;
            //if (centerHit) thrower.needs?.mood?.thoughts?.memories?.TryGainMemory(MoreVanillaStructureDefs.HitBetweenTheEyes);

            Vector3 offset = (miss.y * dir) + (miss.x * left);
            to += offset;
            dir = to - from;
            float distance = dir.magnitude;
            dir.Normalize();
            FleckCreationData data = FleckMaker.GetDataStatic(from, thrower.Map, dart);

            data.velocity = dir * ThrowSpeed;
            data.rotation = (-Mathf.Rad2Deg * Mathf.Atan2(dir.z, dir.x));
            data.airTimeLeft = distance / ThrowSpeed;
            thrower.Map.flecks.CreateFleck(data);

        }

        public static FleckDef GetDartFromIndex(int index)
        {
            switch (index)
            {
                case 0: return BamStructureDefs.Fleck_Dart_Red;
                case 1: return BamStructureDefs.Fleck_Dart_Yellow;
                case 2: return BamStructureDefs.Fleck_Dart_Blue;
                default: return BamStructureDefs.Fleck_Dart_Green;
            }
        }
    }
}
