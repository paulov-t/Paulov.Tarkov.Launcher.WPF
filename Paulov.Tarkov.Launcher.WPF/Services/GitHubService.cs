using Octokit;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Paulov.Launcher.Models.InstallInstanceModel;

namespace Paulov.Launcher.Services
{
    public class GitHubService
    {
        public GitHubService(EGameType gameType)
        {
            this.GameType = gameType;
        }

        public EGameType GameType { get; }

        public async Task<ObservableCollection<Release>> GetSITReleases()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("Paulov.Launcher"));
                switch (GameType)
                {
                    case EGameType.Tarkov:
                        return new ObservableCollection<Release>(await github.Repository.Release.GetAll("stayintarkov", "StayInTarkov.Client", new ApiOptions() { }));
                    case EGameType.TarkovArena:
                        return new ObservableCollection<Release>(await github.Repository.Release.GetAll("paulov-t", "StayInTarkov.Arena.Client", new ApiOptions() { }));
                }
            }
            catch (Exception)
            {

            }

            return new ObservableCollection<Release>();
        }

        public async Task<ObservableCollection<Release>> GetPaulovMinimalReleaseLatest()
        {
            try
            {
                var github = new GitHubClient(new ProductHeaderValue("Paulov.Launcher"));
                switch (GameType)
                {
                    case EGameType.Tarkov:
                        return new ObservableCollection<Release>(new[] { await github.Repository.Release.GetLatest("paulov-t", "Paulov.Tarkov.Minimal") });
                    case EGameType.TarkovArena:
                        throw new Exception("Not implemented");
                }
            }
            catch (Exception)
            {

            }

            return new ObservableCollection<Release>();
        }
    }
}
