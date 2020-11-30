using System.Reflection;
using Commerce.Plugin.ConditionalConfigs.Pipelines.BootstrapPipeline.Blocks;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.Commerce.Core;
using Sitecore.Framework.Configuration;
using Sitecore.Framework.Pipelines.Definitions.Extensions;

namespace Commerce.Plugin.ConditionalConfigs
{
    public class ConfigureSitecore : IConfigureSitecore
    {
        public void ConfigureServices(IServiceCollection services)
        {
            var assembly = Assembly.GetExecutingAssembly();
            services.RegisterAllPipelineBlocks(assembly);

            services.Sitecore().Pipelines(config => config
                .ConfigurePipeline<IBootstrapPipeline>(configure => configure
                        .Replace<BootStrapImportJsonsBlock, ConditionalBootStrapImportJsonsBlock>()
                )
            );
        }
    }
}
