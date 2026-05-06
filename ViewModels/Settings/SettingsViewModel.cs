using System.ComponentModel.DataAnnotations;

namespace PmesCSharp.ViewModels.Settings;

public class SettingsViewModel
{
    [Required]
    [RegularExpression("^(light|dark)$")]
    public string Theme { get; set; } = "dark";
}
