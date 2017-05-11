﻿using System;
using System.ComponentModel.Composition;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using GitHub.Extensions;
using GitHub.Models;
using GitHub.Primitives;
using GitHub.Services;

namespace GitHub.InlineReviews.Services
{
    [Export(typeof(IPullRequestSessionManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class PullRequestSessionManager : IPullRequestSessionManager, IDisposable
    {
        readonly IPullRequestService service;
        readonly IRepositoryHosts hosts;
        readonly ITeamExplorerServiceHolder teamExplorerService;
        readonly BehaviorSubject<IPullRequestSession> currentSession = new BehaviorSubject<IPullRequestSession>(null);
        ILocalRepositoryModel repository;
        PullRequestSession session;

        [ImportingConstructor]
        public PullRequestSessionManager(
            IPullRequestService service,
            IRepositoryHosts hosts,
            ITeamExplorerServiceHolder teamExplorerService)
        {
            Guard.ArgumentNotNull(service, nameof(service));
            Guard.ArgumentNotNull(hosts, nameof(hosts));
            Guard.ArgumentNotNull(teamExplorerService, nameof(teamExplorerService));

            this.service = service;
            this.hosts = hosts;
            this.teamExplorerService = teamExplorerService;
            teamExplorerService.Subscribe(this, RepoChanged);
        }

        public IObservable<IPullRequestSession> CurrentSession => currentSession;

        public void Dispose()
        {
            currentSession.Dispose();
            GC.SuppressFinalize(this);
        }

        public async Task<IPullRequestSession> GetSession(IPullRequestModel pullRequest)
        {
            if (pullRequest.Number == session?.PullRequest.Number)
            {
                return session;
            }
            else
            {
                var modelService = hosts.LookupHost(HostAddress.Create(repository.CloneUrl))?.ModelService;

                return new PullRequestSession(
                    await modelService.GetCurrentUser(),
                    pullRequest,
                    repository);
            }
        }


        async void RepoChanged(ILocalRepositoryModel repository)
        {
            PullRequestSession session = null;

            this.repository = repository;

            if (repository != null)
            {
                var modelService = hosts.LookupHost(HostAddress.Create(repository.CloneUrl))?.ModelService;

                if (modelService != null)
                {
                    var pullRequest = await GetPullRequestForTip(modelService, repository);

                    if (pullRequest != null)
                    {
                        session = new PullRequestSession(
                            await modelService.GetCurrentUser(),
                            pullRequest,
                            repository);
                    }
                }
            }

            this.session = session;
            currentSession.OnNext(this.session);
        }

        async Task<IPullRequestModel> GetPullRequestForTip(IModelService modelService, ILocalRepositoryModel repository)
        {
            var number = await service.GetPullRequestForCurrentBranch(repository);
            return number != 0 ?
                await modelService.GetPullRequest(repository, number).ToTask() :
                null;
        }
    }
}