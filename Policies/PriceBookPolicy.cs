using Sitecore.Commerce.Core;

namespace Feature.Pricing.Engine.Policies
{
    public class PriceBookPolicy : Policy
	{
		/// <summary>
		/// Override Price Book associated to Catalog with Price Book associated to Commerce Shop
		/// </summary>
		public bool UseCommerceShopPriceBook { get; set; }
	}
}
