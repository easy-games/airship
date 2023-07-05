using UnityEngine;

namespace Assets.Code.Misc
{
	public static class LayerUtil
	{
		/// <summary>
		/// Converts a layer mask to a layer number.
		/// </summary>
		/// <param name="mask"></param>
		/// <returns></returns>
		public static int LayerMaskToLayerNumber(LayerMask mask)
		{
			return LayerValueToLayerNumber(mask.value);
		}

		/// <summary>
		/// Converts a layer value int to a layer int.
		/// </summary>
		/// <param name="bitmask"></param>
		/// <returns></returns>
		public static int LayerValueToLayerNumber(int bitmask)
		{
			var result = bitmask > 0 ? 0 : 31;
			while (bitmask > 1)
			{
				bitmask >>= 1;
				result++;
			}
			return result;
		}

		/// <summary>
		/// Returns if a LayerMask contains a specified layer.
		/// </summary>
		/// <param name="layerMask">LayerMask to check for layer in.</param>
		/// <param name="layer">Layer to check within LayerMask.</param>
		/// <returns></returns>
		public static bool ContainsLayer(LayerMask layerMask, int layer)
		{
			return (layerMask == (layerMask | (1 << layer)));
		}
	}
}