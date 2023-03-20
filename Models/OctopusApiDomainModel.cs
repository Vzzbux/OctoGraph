using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OctopusVis.OctopusApiDomainModel
{
    public class OctopusInstance
    {
        public Uri ServiceUrl { get; set; }
        public List<Space> Spaces { get; set; }
        public List<Machine> Machines { get; set; }
        public List<DeploymentEnvironment> Environments { get; set; }
        public List<Project> Projects { get; set; }
        public List<ProjectGroup> ProjectGroups { get; set; }
        public List<Team> Teams { get; set; }
        public List<User> Users { get; set; }
        public static Task<OctopusInstance> CreateAsync(HttpClient client)
        {
            var ret = new OctopusInstance();
            return ret.InitializeAsync(client);
        }

        private async Task<OctopusInstance> InitializeAsync(HttpClient _client)
        {
            ServiceUrl = _client.BaseAddress;

            var spacesTask = _client.GetFromJsonAsync<List<Space>>("/api/spaces/all");
            var machinesTask = _client.GetFromJsonAsync<List<Machine>>("/api/machines/all");
            var environmentsTask = _client.GetFromJsonAsync<List<DeploymentEnvironment>>("/api/environments/all");
            var projectsTask = _client.GetFromJsonAsync<List<Project>>("/api/projects/all");
            var projectGroupsTask = _client.GetFromJsonAsync<List<ProjectGroup>>($"/api/projectgroups/all");
            var teamsTask = _client.GetFromJsonAsync<List<Team>>("/api/teams/all");
            var usersTask = _client.GetFromJsonAsync<List<User>>("/api/users/all?skip=0&take=50000");

            await Task.WhenAll(spacesTask, machinesTask, environmentsTask, projectsTask, projectGroupsTask, teamsTask, usersTask);

            Spaces = spacesTask.Result;
            Machines = machinesTask.Result;
            Environments = environmentsTask.Result;
            Projects = projectsTask.Result;
            ProjectGroups = projectGroupsTask.Result;
            Teams = teamsTask.Result.Where(t => t.Name != "Everyone").ToList();
            Users = usersTask.Result;

            foreach (var machine in Machines.Where(m => m.EnvironmentIds.Any()))
            {
                machine.Environments = Environments.FindAll(e => machine.EnvironmentIds.Contains(e.Id));
            }

            foreach (var team in Teams)
            {
                team.MemberUsers = Users.Where(u => team.MemberUserIds.Contains(u.Id)).ToList();
            }

            foreach (var team in Teams.Where(t => !string.IsNullOrEmpty(t.Links.ScopedUserRoles)).ToList())
            {
                var response = await _client.GetAsync(
                    team.Links.ScopedUserRoles.Replace(@"{?skip,take}", @"?skip=0&take=2147483647"));
                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        Teams.Remove(team);
                        continue;
                    }

                    throw new HttpRequestException(
                        $"Call to ${response.RequestMessage.RequestUri} returned a ${response.StatusCode}: ${response.ReasonPhrase}");
                }

                var scopedUserRoles = await response.Content.ReadFromJsonAsync<ScopedUserRoles>();

                team.ScopedUserRoles = scopedUserRoles;
                team.ProjectGroupIds = scopedUserRoles.Items.SelectMany(i => i.ProjectGroupIds).Distinct().ToList();
                team.ProjectIds = scopedUserRoles.Items.SelectMany(i => i.ProjectIds).Distinct().ToList();
            }

            foreach (var project in Projects.Where(p => !string.IsNullOrEmpty(p.Links.DeploymentProcess)))
            {
                var deploymentProcess = await _client.GetFromJsonAsync<DeploymentProcess>(project.Links.DeploymentProcess);
                project.TargetRoles = deploymentProcess.Steps.Select(s => s.Properties.TargetRoles).Distinct().ToList();
                project.Machines = Machines.Where(m => m.Roles.Any(r => project.TargetRoles.Contains(r))).Distinct()
                    .ToList();
                project.Teams = Teams.Where(t => t.ProjectIds.Contains(project.Id)).ToList();
            }

            foreach (var projectGroupId in Projects.Select(p => p.ProjectGroupId).Distinct())
            {
                var projectGroup = ProjectGroups.FirstOrDefault(pg => pg.Id == projectGroupId);
                if (projectGroup == null) continue;
                projectGroup.Projects = Projects.Where(p => p.ProjectGroupId == projectGroupId).ToList();
                projectGroup.Teams = Teams.Where(t => t.ProjectGroupIds.Contains(projectGroupId)).ToList();
                projectGroup.SysAdminTeams = Teams.Where(t => t.ScopedUserRoles.Items.Any(ur => ScopedUserRoles.AdminRoles.Contains(ur.UserRoleId))
                                                                      || t.ScopedUserRoles.Items.Any(ur => ur.UserRoleId == "userroles-spacemanager" && ur.SpaceId == projectGroup.SpaceId)).ToList();
            }

            return this;
        }
    }

    public class Space
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public string Url => $"app#/{Id}/configuration/spaces/{Id}";
    }

    public class Machine
    {
        public string Id { get; set; }
        public List<string> Roles { get; set; }
        public string Name { get; set; }
        public string SpaceId { get; set; }

        private Uri _url;
        public Uri Uri
        {
            get { return _url; }
            set
            {
                _url = value;
                if (value != null)
                {
                    try
                    {
                        IPHostEntry = Dns.GetHostEntry(value.Host);
                    }
                    catch (System.Net.Sockets.SocketException)
                    {
                    }
                }
            }
        }
        private IPHostEntry IPHostEntry { get; set; }
        public string Hostname => IPHostEntry != null ? IPHostEntry.HostName : Uri?.Host;
        public string[] Aliases => IPHostEntry?.Aliases ?? Array.Empty<string>();

        public List<string> EnvironmentIds { get; set; }
        public List<DeploymentEnvironment> Environments { get; set; }
        public DeploymentEnvironmentEnum? Environment => DeploymentEnvironment.GetDeploymentEnvironmentEnum(Environments);

        public string Url => $"app#/{SpaceId}/infrastructure/machines/{Id}";
    }

    public enum DeploymentEnvironmentEnum
    {
        Production,
        Preproduction,
        Development,
        Other
    }

    public class DeploymentEnvironment
    {
        public static readonly string[] ProductionStrings = new[] { "production", "prod", "live" };
        public static readonly string[] PreProductionStrings = new[] { "preproduction", "preprod", "test", "uat", "staging" };
        public static readonly string[] DevStrings = new[] { "development", "dev" };

        private static readonly Regex rgx = new("[^a-zA-Z0-9]");

        public string Id { get; set; }
        public string Name { get; set; }

        public string CleanName => rgx.Replace(Name, "");

        public static DeploymentEnvironmentEnum? GetDeploymentEnvironmentEnum(IEnumerable<DeploymentEnvironment> environments)
        {
            if (environments.Any(e =>
                DeploymentEnvironment.ProductionStrings.Contains(e.CleanName, StringComparer.OrdinalIgnoreCase)))
            {
                return DeploymentEnvironmentEnum.Production;
            }
            if (environments.Any(e =>
                DeploymentEnvironment.PreProductionStrings.Contains(e.CleanName, StringComparer.OrdinalIgnoreCase)))
            {
                return DeploymentEnvironmentEnum.Preproduction;
            }
            if (environments.Any(e =>
                DeploymentEnvironment.DevStrings.Contains(e.CleanName, StringComparer.OrdinalIgnoreCase)))
            {
                return DeploymentEnvironmentEnum.Development;
            }
            if (environments.Any())
            {
                return DeploymentEnvironmentEnum.Other;
            }

            return null;
        }
    }

    public class Project
    {
        public string Id { get; set; }
        public string ProjectGroupId { get; set; }
        public string SpaceId { get; set; }
        public string Name { get; set; }
        public Links Links { get; set; }
        public List<string> TargetRoles { get; set; }
        public List<Machine> Machines { get; set; }
        public List<Team> Teams { get; set; }

        public string Url => $"app#/{SpaceId}/projects/{Id}";
    }

    public class User
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public bool IsActive { get; set; }
        public bool IsService { get; set; }
        public string Url => $"app#/configuration/users/{Id}";
    }

    public class Links
    {
        public string DeploymentProcess { get; set; }
        public string ScopedUserRoles { get; set; }
    }

    public class DeploymentProcess
    {
        public List<Step> Steps { get; set; }
    }

    public class Step
    {
        public Properties Properties { get; set; }
    }

    public class Properties
    {
        [JsonPropertyName("Octopus.Action.TargetRoles")]
        public string TargetRoles { get; set; }
    }

    public class ProjectGroup
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SpaceId { get; set; }
        public List<Project> Projects { get; set; } = new List<Project>();
        public List<Team> Teams { get; set; } = new List<Team>();
        public List<Team> SysAdminTeams { get; set; } = new List<Team>();

        public string Url => $"app#/{SpaceId}/projectGroups/{Id}";
        public IEnumerable<Team> AllProjectTeams => Teams.Union(Projects.SelectMany(p => p.Teams)).Distinct();
        public IEnumerable<Team> AllTeams => AllProjectTeams.Union(SysAdminTeams).Distinct();

        public IEnumerable<Team> AllNonEmptyProjectTeams => AllProjectTeams.Where(t => t.HasMembers);
        public IEnumerable<Team> AllNonEmptyTeams => AllTeams.Where(t => t.HasMembers);
    }

    public class Team
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Links Links { get; set; }
        public List<string> MemberUserIds { get; set; }
        public List<User> MemberUsers { get; set; }
        public List<ExternalSecurityGroup> ExternalSecurityGroups { get; set; }
        public List<string> ProjectGroupIds { get; set; }
        public List<string> ProjectIds { get; set; }
        public ScopedUserRoles ScopedUserRoles { get; set; }
        public string SpaceId { get; set; }

        public bool HasMembers => MemberUsers.Any(u => u.IsActive && !u.IsService) || ExternalSecurityGroups.Any();

        public string Url => SpaceId != null ? $"app#/{SpaceId}/configuration/teams/{Id}" : $"app#/configuration/teams/{Id}";
    }

    public class ExternalSecurityGroup
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
    }
    public class ScopedUserRoles
    {
        public static readonly string[] AdminRoles = new string[]
            {"userroles-systemadministrator", "userroles-systemmanager"};
        public List<Item> Items { get; set; }
    }

    public class Item
    {
        public string UserRoleId { get; set; }
        public string SpaceId { get; set; }
        public List<string> ProjectGroupIds { get; set; }
        public List<string> ProjectIds { get; set; }
    }
}


