public static class ConditionMapper
{
    public static KitchenHighlightManager.TargetingMode FromLetter(string letter)
    {
        return letter switch
        {
            "a" => KitchenHighlightManager.TargetingMode.None,
            "b" => KitchenHighlightManager.TargetingMode.All,
            "c" => KitchenHighlightManager.TargetingMode.Subset,
            "d" => KitchenHighlightManager.TargetingMode.Sequential,
            _   => KitchenHighlightManager.TargetingMode.None
        };
    }
}