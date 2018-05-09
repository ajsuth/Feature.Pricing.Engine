using Microsoft.Extensions.Logging;
using Sitecore.Commerce.Core;
using Sitecore.Commerce.Plugin.Management;
using Sitecore.Framework.Caching;
using Sitecore.Framework.Pipelines;
using Sitecore.Services.Core.Model;
using System;
using System.Linq;
using System.Threading.Tasks;
using Feature.Pricing.Engine.Policies;

namespace Feature.Pricing.Engine.Pipelines.Blocks
{
	[PipelineDisplayName(PricingConstants.Pipelines.Blocks.ResolvePriceBookNameFromCommerceShopBlock)]
	public class ResolvePriceBookNameFromCommerceShopBlock : PipelineBlock<string, string, CommercePipelineExecutionContext>
	{
		private readonly IGetEnvironmentCachePipeline _cachePipeline;
		private readonly IGetItemByPathPipeline _getItemByPathPipeline;

		public ResolvePriceBookNameFromCommerceShopBlock(IGetEnvironmentCachePipeline cachePipeline, IGetItemByPathPipeline getItemByPathPipeline)
		{
			_cachePipeline = cachePipeline;
			_getItemByPathPipeline = getItemByPathPipeline;
		}

		public override async Task<string> Run(string arg, CommercePipelineExecutionContext context)
		{
			var priceBookName = arg;
			if(!context.GetPolicy<PriceBookPolicy>().UseCommerceShopPriceBook)
			{
				return await Task.FromResult(priceBookName);
			}

			Sitecore.Commerce.Plugin.Shops.Shop shop = context.CommerceContext.Objects.OfType<Sitecore.Commerce.Plugin.Shops.Shop>().FirstOrDefault();
			if (shop == null)
			{
				return await Task.FromResult(priceBookName);
			}

			string storefrontItemPath = $"{context.GetPolicy<SitecoreControlPanelItemsPolicy>().StorefrontsPath}/{shop.Name}";
			string cacheKey = storefrontItemPath;
			ManagementCachePolicy cachePolicy = context.GetPolicy<ManagementCachePolicy>();
			ICache cache = null;
			ItemModel storefrontItemModel;
			if (cachePolicy.AllowCaching)
			{
				EnvironmentCacheArgument environmentCacheArgument = new EnvironmentCacheArgument();
				environmentCacheArgument.CacheName = cachePolicy.ItemsCacheName;
				cache = await _cachePipeline.Run(environmentCacheArgument, context);
				storefrontItemModel = await cache.Get(cacheKey) as ItemModel;
				if (storefrontItemModel != null)
				{
					return await Task.FromResult(ResolvePriceBookName(priceBookName, GetPricebookName(storefrontItemModel)));
				}
			}

			context.Logger.LogInformation($"Feature.Pricing.GetStorefront.{cacheKey}");

			try
			{
				ItemModelArgument itemModelArgument = new ItemModelArgument(storefrontItemPath);
				itemModelArgument.Language = context.GetPolicy<GlobalEnvironmentPolicy>().DefaultLocale;
				storefrontItemModel = await _getItemByPathPipeline.Run(itemModelArgument, context);
				if (storefrontItemModel != null)
				{
					if (cachePolicy.AllowCaching)
					{
						await cache.Set(cacheKey, new Cachable<ItemModel>(storefrontItemModel, 1L), cachePolicy.GetCacheEntryOptions());
					}

					return await Task.FromResult(ResolvePriceBookName(priceBookName, GetPricebookName(storefrontItemModel)));
				}
			}
			catch (Exception ex)
			{
				string str = await context.CommerceContext.AddMessage(context.GetPolicy<KnownResultCodes>().Error, "CommerceTermsNotFound", new object[2]
				{
					storefrontItemPath,
					ex
				}, string.Format("Sitecore commerce terms at {0} were not found.", storefrontItemPath));
			}

			return await Task.FromResult(priceBookName);
		}

		private string ResolvePriceBookName(string initialPriceBookName, string commerceShopPriceBookName)
		{
			return string.IsNullOrWhiteSpace(commerceShopPriceBookName) ? initialPriceBookName : commerceShopPriceBookName;
		}

		private string GetPricebookName(ItemModel storefrontItemModel)
		{
			return storefrontItemModel[PricingConstants.Fields.Pricebook] as string;
		}
	}
}
