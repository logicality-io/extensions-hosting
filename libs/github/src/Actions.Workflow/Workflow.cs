﻿using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.RepresentationModel;

namespace Logicality.GitHub.Actions.Workflow;

public class Workflow
{
    public const string Header = "# This was generated by tool. Edits will be overwritten.";

    private readonly string                         _name;
    private readonly Dictionary<string, Permission> _permissions      = new();
    private readonly Dictionary<string, Job>        _jobs             = new();
    private          PermissionConfig               _permissionConfig = PermissionConfig.NotSpecified;
    private          string?                        _concurrencyGroup;
    private          bool                           _concurrencyCancelInProgress;
    private          IDictionary<string, string>    _env                   = new Dictionary<string, string>();
    private          IDictionary<string, string>    _defaults              = new Dictionary<string, string>();
    private          string                         _defaultsRunShell      = string.Empty;
    private          string                         _defaultsRunWorkingDir = string.Empty;

    public Workflow(string name)
    {
        _name = name;
        On    = new On(this);
    }

    public On On { get; }

    public Workflow Permissions(
        Permission actions            = Permission.None,
        Permission checks             = Permission.None,
        Permission contents           = Permission.None,
        Permission deployments        = Permission.None,
        Permission discussions        = Permission.None,
        Permission idToken            = Permission.None,
        Permission issues             = Permission.None,
        Permission packages           = Permission.None,
        Permission pages              = Permission.None,
        Permission pullRequests       = Permission.None,
        Permission repositoryProjects = Permission.None,
        Permission securityEvents     = Permission.None,
        Permission statuses           = Permission.None)
    {
        _permissions[PermissionKeys.Actions]            = actions;
        _permissions[PermissionKeys.Checks]             = checks;
        _permissions[PermissionKeys.Contents]           = contents;
        _permissions[PermissionKeys.Deployments]        = deployments;
        _permissions[PermissionKeys.Discussions]        = discussions;
        _permissions[PermissionKeys.IdToken]            = idToken;
        _permissions[PermissionKeys.Issues]             = issues;
        _permissions[PermissionKeys.Packages]           = packages;
        _permissions[PermissionKeys.Pages]              = pages;
        _permissions[PermissionKeys.PullRequests]       = pullRequests;
        _permissions[PermissionKeys.RepositoryProjects] = repositoryProjects;
        _permissions[PermissionKeys.SecurityEvents]     = securityEvents;
        _permissions[PermissionKeys.Statuses]           = statuses;

        _permissionConfig = PermissionConfig.Custom;

        return this;
    }

    public Workflow PermissionsReadAll()
    {
        _permissionConfig = PermissionConfig.ReadAll;
        return this;
    }

    public Workflow PermissionsWriteAll()
    {
        _permissionConfig = PermissionConfig.WriteAll;
        return this;
    }

    public Workflow Concurrency(string @group, bool cancelInProgress = false)
    {
        _concurrencyGroup            = @group;
        _concurrencyCancelInProgress = cancelInProgress;
        return this;
    }

    public Workflow Env(IDictionary<string, string> environment)
    {
        _env = environment;
        return this;
    }

    public Workflow Defaults(IDictionary<string, string> defaults)
    {
        _defaults = defaults;
        return this;
    }

    public Workflow DefaultsRun(string shell, string workingDirectory)
    {
        _defaultsRunShell      = shell;
        _defaultsRunWorkingDir = workingDirectory;
        return this;
    }

    public Job Job(string id)
    {
        var job = new Job(id, this);
        _jobs.Add(id, job);
        return job;
    }

    public string GetYaml(SequenceStyle sequenceStyle = SequenceStyle.Block) 
    {
        var rootNode = new YamlMappingNode(
            new YamlScalarNode("name"),
            new YamlScalarNode(_name)
        );
        var yamlDocument = new YamlDocument(rootNode);

        // Triggers
        On.Build(rootNode, sequenceStyle);

        // Permissions
        PermissionHelper.BuildPermissionsNode(rootNode, _permissionConfig, _permissions);

        // Concurrency
        if (!string.IsNullOrWhiteSpace(_concurrencyGroup))
        {
            var concurrencyMappingNode = new YamlMappingNode
            {
                { "group", _concurrencyGroup },
                { "cancel-in-progress", _concurrencyCancelInProgress.ToString().ToLower() }
            };
            rootNode.Add("concurrency", concurrencyMappingNode);
        }

        // Env
        if (_env.Any())
        {
            var envMappingNode = new YamlMappingNode();
            foreach (var env in _env)
            {
                envMappingNode.Add(env.Key, new YamlScalarNode(env.Value));
            }
            rootNode.Add("env", envMappingNode);
        }

        // Defaults
        if (_defaults.Any() || !string.IsNullOrWhiteSpace(_defaultsRunShell))
        {
            var defaultsMappingNode = new YamlMappingNode();
            foreach (var @default in _defaults)
            {
                defaultsMappingNode.Add(@default.Key, new YamlScalarNode(@default.Value));
            }
            // Defauls Run
            if (!string.IsNullOrWhiteSpace(_defaultsRunShell))
            {
                var defaultsRunMappingNode = new YamlMappingNode()
                {
                    { "shell", _defaultsRunShell },
                    { "working-directory", _defaultsRunWorkingDir }
                };
                defaultsMappingNode.Add("run", defaultsRunMappingNode);
            }
            rootNode.Add("defaults", defaultsMappingNode);
        }

        // Jobs
        if (_jobs.Any())
        {
            var jobsNode = new YamlMappingNode();
            foreach (var job in _jobs)
            {
                job.Value.Build(jobsNode, sequenceStyle);
            }

            rootNode.Add("jobs", jobsNode);
        }

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("# This was generated by tool. Edits will be overwritten.");
        stringBuilder.AppendLine();
        var stringWriter    = new StringWriter(stringBuilder);
        var yamlStream      = new YamlStream(yamlDocument);
        var emitterSettings = new EmitterSettings();
        var emitter         = new Emitter(stringWriter, emitterSettings);
        yamlStream.Save(emitter, false);

        // YamlDotnet inserts a "{}" for empty mapping. Just want empty string.
        var yaml = stringBuilder.ToString();
        yaml = yaml.Replace(" {}", string.Empty);

        // HACK For some reason yamldotnet adds a ".../r/n" to the end. This removes it.
        // Maybe I'm missing something...
        yaml = yaml.Remove(yaml.Length - 5, 5);

        return yaml;
    }
}