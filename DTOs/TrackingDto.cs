using System.ComponentModel.DataAnnotations;

namespace Commute360.DTOs;

public class UpdateLocationDto
{
    public int RouteId { get; set; }

    [Range(-90, 90)]
    public double Latitude { get; set; }

    [Range(-180, 180)]
    public double Longitude { get; set; }

    [Range(0, 360)]
    public double? Heading { get; set; }

    [Range(0, 300)]
    public double? SpeedKmh { get; set; }
}

public class CreateAlertDto
{
    public int RouteId { get; set; }

    [Required]
    [RegularExpression("^(Delay|Traffic|Breakdown|Info|Emergency)$",
        ErrorMessage = "Type must be Delay, Traffic, Breakdown, Info or Emergency.")]
    public string Type { get; set; } = "Info";

    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Message { get; set; } = "";
}