using DV.Booklets;
using HarmonyLib;
using DV.ThingTypes;
using UnityEngine;
using System.Collections.Generic;
using DV.RenderTextureSystem.BookletRender;
using System.Linq;
using DV.CabControls.Spec;
using Archipelago.MultiClient.Net.Models;
using System.Text;
using System;
using System.Security.Cryptography;
namespace DvMod.Randomizer
{
    [HarmonyPatch(typeof(BookletCreator_JobReport))]
    public class JobCounter {
        private static void AddData(ref List<TemplatePaperData> mainList, List<JobReportTasksTemplatePaperData.JobReportEntry> ToAdd) {
            if (ToAdd.Count == 0) return;
            TemplatePaperData lastPage = mainList[mainList.Count-2];
            List<JobReportTasksTemplatePaperData.JobReportEntry> missing;
            int canFit;
            switch (lastPage) {
                case JobReportOverviewTemplatePaperData p : 
                canFit = Math.Min(ToAdd.Count, 5-p.reportEntries.Count);
                p.reportEntries.AddRange(ToAdd.Take(canFit));
                missing = [.. ToAdd.Skip(canFit)];
                break;
                case JobReportTasksTemplatePaperData p : 
                canFit = Math.Min(ToAdd.Count, 9-p.reportEntries.Count);
                p.reportEntries.AddRange(ToAdd.Take(canFit));
                missing = [.. ToAdd.Skip(canFit)];
                break;
                default :
                missing = ToAdd;
                break;
            };
            if (missing.Count > 0) {
                int PagesToAdd = missing.Count / 9 + (missing.Count % 9 > 0 ? 1 : 0);
                JobReportPaymentTemplatePaperData lastPageData = (JobReportPaymentTemplatePaperData) mainList.Last();
                string newTotalPages = (int.Parse(lastPageData.totalPages) + PagesToAdd).ToString();
                int newLastPage = int.Parse(lastPageData.pageNumber) + PagesToAdd;
                foreach (TemplatePaperData middlePage in mainList) {
                    switch (middlePage) {
                        case JobReportOverviewTemplatePaperData p: p.totalPages = newTotalPages; break;
                        case JobReportTasksTemplatePaperData p: p.totalPages = newTotalPages; break;
                        case JobReportPaymentTemplatePaperData p: p.totalPages = newTotalPages; p.pageNumber = newLastPage.ToString(); break;
                    }
                }
                mainList.InsertRange(mainList.Count-1, 
                    Enumerable.Range(0, PagesToAdd).Select(
                        i => new JobReportTasksTemplatePaperData([.. ToAdd.Skip(9*i).Take(9)], (newLastPage - PagesToAdd + i).ToString(), newTotalPages)
                    )
                );
                
            }
        }

        [HarmonyPatch("GetReportTemplateData")]
        public static void Postfix(ref List<TemplatePaperData> __result, Job_data data) {
            if (Main.player == null) return;
            if (data.state != JobState.Completed) return;
            JobFinishState jobState = Main.player.FinishJob(data);
            Main.Log("I got data!");
            Main.Log($"What is inside: {jobState.HasWon}/{jobState.IsShunting}/{jobState.Item}/{jobState.RemainingForVictory}/{jobState.RemainingJobs}");
            List<JobReportTasksTemplatePaperData.JobReportEntry> ToAdd = [];
            if (jobState.RemainingForVictory >= 0){
                StringBuilder sb = new();
                if (jobState.Item != null) {
                    sb.AppendLine($"You got a {jobState.Item.ItemDisplayName} for {jobState.Item.Player.Name}.");
                }
                string job = jobState.IsShunting?"shunting":"transport";
                int remaining = jobState.RemainingJobs;
                if (remaining > 0) 
                    sb.AppendLine($"There are {remaining} rewards left for {job} here");
                else 
                    sb.AppendLine($"You got all rewards for {job} here");
                ToAdd.Add(new(sb.ToString(), "", remaining > 0?JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS:JobReportTasksTemplatePaperData.EntryState.COMPLETED));
            } else {
                ToAdd.Add(new("You do not have the required station license. You cannot earn any item", "", JobReportTasksTemplatePaperData.EntryState.WARNING));
            }
            if (jobState.HasWon) {
                ToAdd.Add(new("You have completed the game!", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
            } else if (jobState.RemainingForVictory == 0) {
                ToAdd.Add(new("You have not won yet, but you finished enough jobs in this station","", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
            } else if (jobState.RemainingForVictory > 0) {
                ToAdd.Add(new($"You need {jobState.RemainingForVictory} jobs to finish this station", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
            } else {
                ToAdd.Add(new("You cannot progress towards victory here", "", JobReportTasksTemplatePaperData.EntryState.WARNING));
            }
            Main.Log("I've finished the first part");
            if (PlayerManager.LastLoco == null) {
                ToAdd.Add(new("Could not find your last loco", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS_WITH_X_MARK));
            } else {
                TrainCarType LastLoco = PlayerManager.LastLoco.carType;
                (long checkLoco, int remainingLoco) = Main.player.FinishLoco(PlayerManager.LastLoco.carType);
                if (remainingLoco < 0)
                    ToAdd.Add(new("You already got the reward for using the "+RandoCommonData.GetLocoNameFromType(LastLoco), "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                else if (remainingLoco == 0) {
                    ItemInfo LocoItem = Main.player.UnlockCheck(checkLoco);
                    ToAdd.Add(new("You finished enough job with a "+RandoCommonData.GetLocoNameFromType(LastLoco)+" to get a "+LocoItem.ItemDisplayName, "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                } else
                    ToAdd.Add(new($"You still need {remainingLoco} jobs with a {RandoCommonData.GetLocoNameFromType(LastLoco)} to get a reward", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
            }
            AddData(ref __result, ToAdd);
        }
    
    }
}