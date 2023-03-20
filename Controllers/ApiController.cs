using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OctoGraph.Models;
using OctopusVis.OctopusApiDomainModel;
using OctopusVis.VisNetworkModel;

namespace OctoGraph.Controllers;

[ApiController]
[Authorize]
public class ApiController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<ApiController> _logger;
    private readonly IMemoryCache _cache;
    private readonly OctoGraphOptions _options;

    public ApiController(ILogger<ApiController> logger, IHttpClientFactory clientFactory, IMemoryCache memoryCache, IOptions<OctoGraphOptions> options)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _cache = memoryCache;
        _options = options.Value;
    }

    private async Task<OctopusInstance> GetOctopusInstance(string octopusServerLabel, bool forceCacheRefresh)
    {
        var cacheKey = $"OctopusInstance_{octopusServerLabel.Trim().ToLower()}";
        var test = _cache.Get(cacheKey);
        _logger.LogDebug(test != null ? $"Cache hit for {cacheKey}" : $"Cache miss for {cacheKey}");

        if (forceCacheRefresh)
        {
            var octopus = await GetOctopusInstance(octopusServerLabel);
            _cache.Set(cacheKey, octopus);
            return octopus;
        }

        return await _cache.GetOrCreateAsync(cacheKey, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(_options.CacheTimeoutInMinutes);
            return GetOctopusInstance(octopusServerLabel);
        });
    }

    private async Task<OctopusInstance> GetOctopusInstance(string octopusServerLabel)
    {
        var _client = _clientFactory.CreateClient(octopusServerLabel);
        return await OctopusInstance.CreateAsync(_client);
    }

    [HttpGet]
    [Route("/api/octopi")]
    public Dictionary<string, string> GetAvailableOctopusEnvironments()
    {
        return _options.OctopusInstances.ToDictionary(o => o.Label, o => o.Label);
    }

    [HttpGet]
    [Route("/api/graph")]
    public async Task<RootObjectWrapper> Graph(string octopusServerLabel, bool forceCacheRefresh = false, bool linkSysAdminTeams = false, bool showUsers = false, bool showSpaces = false)
    {
        var octopus = await GetOctopusInstance(octopusServerLabel, forceCacheRefresh);

        //** NODES **
        var nodes = new List<Node>();
        if (showSpaces)
        {
            nodes.AddRange(octopus.Spaces.Select(s => new Node() { Id = s.Id, Label = s.Name, Shape = "diamond", Url = s.Url}));
        }
        nodes.AddRange(octopus.ProjectGroups.Select(g => new Node() { Id = g.Id, Label = g.Name, Shape = "circle", Url = g.Url }).ToList());
        nodes.AddRange(octopus.ProjectGroups.SelectMany(g => g.Projects).Select(g => new Node() { Id = g.Id, Label = g.Name, Group = g.ProjectGroupId, Shape = "ellipse", Url = g.Url }).ToList());
        nodes.AddRange(octopus.Machines.Select(m => new Node()
        {
            Id = m.Id,
            Label = m.Name,
            Group = m.Environment?.ToString().ToLower(),
            PopupText = $"Hostname:{m.Hostname}\r\nAliases:{string.Join("; ", m.Aliases)}\r\nEnvironments: {string.Join(", ", m.Environments.Select(e => e.Name))}\r\nRoles: {string.Join(", ", m.Roles)}",
            Url = m.Url
        }).ToList());
        nodes.AddRange(octopus.Teams.Where(t => t.HasMembers).Select(g => new Node() { Id = g.Id, Label = g.Name, Group = "teams", Url = g.Url }).ToList());
        if (showUsers)
        {
            nodes.AddRange(octopus.Users.Where(t => t.IsActive && !t.IsService).Select(t =>
                new Node() { Id = t.Id, Label = t.DisplayName, Group = "users", Url = t.Url }));
        }

        //** EDGES **
        var edges = new List<Edge>();
        foreach (var projectGroup in octopus.ProjectGroups)
        {
            //Join project groups to projects
            edges.AddRange(projectGroup.Projects.Select(project => new Edge() { From = projectGroup.Id, To = project.Id }));
            if (linkSysAdminTeams)
            {
                edges.AddRange(projectGroup.AllNonEmptyTeams.Select(team => new Edge()
                { From = team.Id, To = projectGroup.Id }));
            }
            else
            {
                //Join teams to project groups
                edges.AddRange(projectGroup.AllNonEmptyProjectTeams.Select(team => new Edge()
                { From = team.Id, To = projectGroup.Id }));
            }

            if (showSpaces)
            {
                //Join project group to Space
                edges.Add(new Edge() { From = projectGroup.SpaceId, To = projectGroup.Id });
            }
        }

        foreach (var project in octopus.ProjectGroups.SelectMany(g => g.Projects))
        {
            //Join projects to servers
            edges.AddRange(project.Machines.Select(machine => new Edge() { From = project.Id, To = machine.Id, Label = string.Join(",\r\n", project.TargetRoles.Intersect(machine.Roles)) }));
        }

        if (showUsers)
        {
            foreach (var team in octopus.Teams.Where(t => t.HasMembers))
            {
                //Join users to teams
                edges.AddRange(team.MemberUsers.Where(u => u.IsActive && !u.IsService)
                    .Select(user => new Edge() { From = team.Id, To = user.Id }));
            }
        }

        if (showSpaces)
        {
            foreach (var team in octopus.Teams.Where(t => !String.IsNullOrEmpty(t.SpaceId)))
            {
                edges.Add(new Edge() { From = team.Id, To = team.SpaceId });
            }
        }

        return new RootObjectWrapper()
        {
            Nodes = nodes,
            Edges = edges,
            ServiceUrl = octopus.ServiceUrl.ToString(),
        };
    }
}