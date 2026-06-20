using DV.Booklets;
using HarmonyLib;
using DV.ThingTypes;
using System.Collections.Generic;
using DV.RenderTextureSystem.BookletRender;
using System.Linq;
using System;
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
            }
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
                        i => new JobReportTasksTemplatePaperData([.. missing.Skip(9*i).Take(9)], (newLastPage - PagesToAdd + i).ToString(), newTotalPages)
                    )
                );
                
            }
        }

        [HarmonyPatch("GetReportTemplateData")]
        public static void Postfix(ref List<TemplatePaperData> __result, Job_data data) {
            if (!Main.IsConnected) return;
            if (data.state != JobState.Completed) return;
            JobFinishState jobState = Main.Player.FinishJob(data);
            List<JobReportTasksTemplatePaperData.JobReportEntry> ToAdd = [];
            string job = jobState.IsShunting?"shunting":"transport";
            string otherJob = !jobState.IsShunting?"shunting":"transport";
            if (jobState.GotStationLicense){
                if (jobState.Item_job1 != null) {
                    ToAdd.Add(new($"You got a {jobState.Item_job1.ItemDisplayName} for {jobState.Item_job1.Player.Name} by {job} in {jobState.Station}.", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                }
                if (jobState.Item_job2 != null) {
                    ToAdd.Add(new($"Doubled! You got a {jobState.Item_job2.ItemDisplayName} for {jobState.Item_job2.Player.Name} by {job} in {jobState.Station}.", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                }
                if (jobState.RemainingJobs > 0) 
                    ToAdd.Add(new($"There are {jobState.RemainingJobs} rewards left for {job} in {jobState.Station}", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
                else 
                    ToAdd.Add(new($"You got all rewards for {job} in {jobState.Station}", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                if (jobState.RemainingOtherJobs > 0) 
                    ToAdd.Add(new($"There are {jobState.RemainingOtherJobs} rewards left for {otherJob} in {jobState.Station}", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
                else 
                    ToAdd.Add(new($"You got all rewards for {otherJob} in {jobState.Station}", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                if (jobState.LastCar == null)
                    ToAdd.Add(new("Could not find your last loco", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS_WITH_X_MARK));
                else {
                    string LocoName = RandoCommonData.GetLocoNameFromType(jobState.LastCar.Value);
                    if (jobState.Item_loco != null) 
                        ToAdd.Add(new($"You got a {jobState.Item_loco.ItemDisplayName} for {jobState.Item_loco.Player.Name} for finishing enough jobs with a {LocoName}", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                    else if (jobState.RemainingLoco == 0)
                        ToAdd.Add(new("You already got the reward for driving the "+LocoName, "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
                    else
                        ToAdd.Add(new($"You need to finish {jobState.RemainingLoco} jobs with a {LocoName} to earn a reward", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
                }
                
            } else {
                ToAdd.Add(new("You do not have the required station license. You cannot earn any item for this job", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS_WITH_X_MARK));
            }
            if (jobState.HasWon) {
                ToAdd.Add(new("You have completed the game!", "", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
            } else if (jobState.RemainingForVictory == 0) {
                ToAdd.Add(new("You have completed enough jobs in this station","", JobReportTasksTemplatePaperData.EntryState.COMPLETED));
            } else  {
                ToAdd.Add(new($"You need {jobState.RemainingForVictory} jobs to finish this station", "", JobReportTasksTemplatePaperData.EntryState.IN_PROGRESS));
            } 
            AddData(ref __result, ToAdd);
        }
    
    }
}