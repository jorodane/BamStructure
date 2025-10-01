using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace BamStructure
{
    public class JobDriver_WatchFace : JobDriver
    {
        TargetIndex MirrorIndex => TargetIndex.A;

        public const int MaxTick = 300;
        int totalTick = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.GetTarget(MirrorIndex), job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            totalTick = 0;
            float beauty = pawn.GetStatValue(StatDefOf.Beauty);
            float multiplier = Mathf.Max(0.25f, 0.5f + (beauty * 0.5f));
            float limitTick = MaxTick * multiplier;
            this.FailOnDespawnedOrNull(MirrorIndex);
            Building mirror = job.GetTarget(MirrorIndex).Thing as Building;

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            Toil play = new Toil()
            {
                socialMode = RandomSocialMode.SuperActive,
                handlingFacing = true,
                defaultCompleteMode = ToilCompleteMode.Never
            };

            play.tickAction = () =>
            {
                this.FailOnDespawnedOrNull(MirrorIndex);
                pawn.rotationTracker.FaceTarget(mirror);
                ++totalTick;
                if (totalTick >= limitTick)
                {
                    EndJobWith(JobCondition.Succeeded);
                }
                else if (JoyUtility.JoyTickCheckEnd(pawn, 1, JoyTickFullJoyAction.EndJob, multiplier, mirror)) { ReadyForNextToil(); return; }
            };
            yield return play;
        }
    }
}
