using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BamStructure
{
    public class JobDriver_WatchFace : JobDriver
    {
        TargetIndex MirrorIndex => TargetIndex.A;

        //public const int MaxTick = 300;
        //int totalTick = 0;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed) && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            float beauty = pawn.GetStatValue(StatDefOf.Beauty);
            float multiplier = Mathf.Max(1.0f, 1.0f + (beauty * 0.25f));
            this.FailOnDespawnedOrNull(MirrorIndex);
            Building mirror = job.GetTarget(MirrorIndex).Thing as Building;

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);

            Toil play = new Toil()
            {
                socialMode = RandomSocialMode.SuperActive,
                handlingFacing = true,
                defaultCompleteMode = ToilCompleteMode.Never
            };
            play.initAction = () =>
            {
                pawn.pather.StopDead();
                pawn.rotationTracker.FaceTarget(mirror);
            };
            play.tickAction = () =>
            {
                if (pawn.IsHashIntervalTick(60) && JoyUtility.JoyTickCheckEnd(pawn, 60, JoyTickFullJoyAction.EndJob, multiplier, mirror)) { EndJobWith(JobCondition.Succeeded); return; }
            };
            yield return play;
        }
    }
}
