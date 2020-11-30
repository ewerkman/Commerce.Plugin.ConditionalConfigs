using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Core.Commands;
using Sitecore.Framework.Conditions;
using Sitecore.Framework.Pipelines;
using SearchOption = System.IO.SearchOption;

namespace Commerce.Plugin.ConditionalConfigs.Pipelines.BootstrapPipeline.Blocks
{
    public class ConditionalBootStrapImportJsonsBlock : AsyncPipelineBlock<string, string, CommercePipelineExecutionContext>
    {
        private const string ConditionsKey = "$conditions";
        private const string TypeKey = "$type";

        private readonly NodeContext _nodeContext;
        private readonly ImportEnvironmentCommand _importEnvironmentCommand;
        private readonly ImportPolicySetCommand _importPolicySetCommand;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConditionalBootStrapImportJsonsBlock> _logger;

        public ConditionalBootStrapImportJsonsBlock(NodeContext nodeContext, 
            ImportEnvironmentCommand importEnvironmentCommand, 
            ImportPolicySetCommand importPolicySetCommand, 
            IConfiguration configuration,
            ILogger<ConditionalBootStrapImportJsonsBlock> logger)
        {
            _nodeContext = nodeContext;
            _importEnvironmentCommand = importEnvironmentCommand;
            _importPolicySetCommand = importPolicySetCommand;
            _configuration = configuration;
            _logger = logger;
        }

        public override async Task<string> RunAsync(string arg, CommercePipelineExecutionContext context)
        {
            Condition.Requires<string>(arg).IsNotNull<string>($"{this.Name}: The argument cannot be null.");
            var files = Directory.GetFiles(_nodeContext.WebRootPath + "\\data\\environments", "*.json", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                _logger.LogInformation($"Processing: {file}");
                var content = File.ReadAllText(file);
                var jobject = JObject.Parse(content);
                if (jobject.HasValues)
                {
                    var properties = jobject.Properties();
                    Func<JProperty, bool> typePredicate = (Func<JProperty, bool>)(p => p.Name.Equals(TypeKey, StringComparison.OrdinalIgnoreCase));
                    if (properties.Any<JProperty>(typePredicate))
                    {
                        var type = jobject.Properties().FirstOrDefault<JProperty>(typePredicate);
                        if (string.IsNullOrEmpty(type?.Value?.ToString()))
                        {
                            _logger.LogError($"{this.Name}.Invalid type in json file '{file}'.");
                            break;
                        }

                        jobject = StripJsonBasedOnConditions(context, jobject);

                        if (type.Value.ToString().Contains(typeof(CommerceEnvironment).FullName))
                        {
                            await HandleCommerceEnvironmentConfig(file, content, jobject, context);
                        }
                        else if (type.Value.ToString().Contains(typeof(PolicySet).FullName))
                        {
                            await HandlePolicySetConfig(file, content, jobject, context);
                        }
                        continue;
                    }
                }
                _logger.LogError($"{this.Name}.Invalid json file '{file}'.");
                break;
            }

            return arg;
        }

        private JObject StripJsonBasedOnConditions(CommercePipelineExecutionContext context, JObject jobject)
        {
            List<JToken> removeList = new List<JToken>();

            WalkNodes(jobject, node =>
            {
                Func<JProperty, bool> conditions2Predicate = (Func<JProperty, bool>)(p => p.Name.Equals(ConditionsKey, StringComparison.OrdinalIgnoreCase));
                var conditions = node.Properties().FirstOrDefault<JProperty>(conditions2Predicate)?.Value?.ToObject<IDictionary<string, string>>();
                if (conditions != null && !ConditionsMatch(conditions, context))
                {   // Add this node to the remove list as it didn't meet the conditions
                    _logger.LogWarning($"Removing: {node}");
                    removeList.Add(node);
                }
            });

            // Remove nodes that didn't match the conditions
            foreach (JToken el in removeList)
            {
                el.Remove();
            }

            return jobject;
        }

        private async Task<bool> HandleCommerceEnvironmentConfig(string file,
            string content,
            JObject jobject,
            CommercePipelineExecutionContext context)
        {
            _logger.LogInformation($"{this.Name}.ImportEnvironmentFromFile: File={file}");
            try
            {
                var commerceEnvironment =
                    await _importEnvironmentCommand.Process(context.CommerceContext, jobject.ToString());
                _logger.LogInformation($"{this.Name}.EnvironmentImported: EnvironmentId={commerceEnvironment.Id}|File={file}");
                return true;
            }
            catch (Exception ex)
            {
                context.CommerceContext.LogException($"{this.Name}.ImportEnvironmentFromFile", ex);
            }

            return false;
        }        

        private async Task<bool> HandlePolicySetConfig(string file, string content, JObject jobject,
            CommercePipelineExecutionContext context)
        {
            _logger.LogInformation($"{this.Name}.ImportPolicySetFromFile: File={file}");
            try
            {
                var policySet = await _importPolicySetCommand.Process(context.CommerceContext, jobject.ToString());
                _logger.LogInformation($"{this.Name}.PolicySetImported: PolicySetId={policySet.Id}|File={file}");
                return true;         
            }
            catch (Exception ex)
            {
                context.CommerceContext.LogException($"{this.Name}.ImportPolicySetFromFile", ex);
            }

            return false;
        }

        private bool ConditionsMatch(IDictionary<string, string> conditions, CommercePipelineExecutionContext context)
        {
            foreach (var condition in conditions.Where(c => !string.IsNullOrEmpty(c.Key) && !string.IsNullOrEmpty(c.Value)))
            {
                var configurationValue = _configuration.GetSection($"AppSettings:{condition.Key}")?.Value;
                if (string.IsNullOrEmpty(configurationValue))
                {
                    _logger.LogWarning($"{this.Name}.ConditionsMatch: AppSetting not found for '{condition.Key}'.");
                    return false;
                }
                var regex = new Regex(condition.Value);
                if (!regex.IsMatch(configurationValue))
                {
                    _logger.LogWarning($"{this.Name}.ConditionsMatch: Condition did not match for setting '{condition.Key}' with condition '{condition.Value}'.");
                    return false;
                }
            }
            return true;
        }

        private void WalkNodes(JToken node, Action<JObject> objectAction = null)
        {
            if (node.Type == JTokenType.Object)
            {
                if (objectAction != null)
                {
                    objectAction((JObject)node);
                }

                foreach (JProperty child in node.Children<JProperty>())
                {
                    WalkNodes(child.Value, objectAction);
                }
            }
            else if (node.Type == JTokenType.Array)
            {
                foreach (JToken child in node.Children())
                {
                    WalkNodes(child, objectAction);
                }
            }
        }
    }
}
