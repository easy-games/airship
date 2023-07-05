/// <summary>
/// Indicates the desired behavior for adding accessories.
/// </summary>
public enum AccessoryAddMode {
	/// <summary>
	/// Clear all existing accessories before adding the new ones.
	/// </summary>
	ReplaceAll,
	
	/// <summary>
	/// Any existing accessories in the same slot will be removed.
	/// </summary>
	Replace,
	
	/// <summary>
	/// Accessory will be added to slot, even if other accessories are currently
	/// in the slot. All accessories will be added together.
	/// </summary>
	Additive,
	
	/// <summary>
	/// Accessory will only be added to the slot if there is currently no accessory
	/// in the slot.
	/// </summary>
	AddIfNone,
}
