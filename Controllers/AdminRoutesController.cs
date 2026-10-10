using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Commute360.Data;
using Commute360.DTOs;
using Commute360.Models;

namespace Commute360.Controllers;

[ApiController]
[Route("api/admin/routes")]
[Authorize(Roles = "Admin")]
public class AdminRoutesController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminRoutesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRoute([FromBody] CreateRouteDto dto)
    {
        var route = new Commute360.Models.Route
        {
            Origin = dto.Origin,
            Destination = dto.Destination,
            ScheduleTime = dto.ScheduleTime,
            Capacity = dto.Capacity,
            DriverId = dto.DriverId,
            Stops = dto.Stops.Select(s => new Stop
            {
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Order = s.Order
            }).ToList()
        };

        _context.Routes.Add(route);
        await _context.SaveChangesAsync();

        return Ok(new RouteResponseDto
        {
            Id = route.Id,
            Origin = route.Origin,
            Destination = route.Destination,
            ScheduleTime = route.ScheduleTime,
            Capacity = route.Capacity,
            DriverId = route.DriverId,
            Stops = route.Stops.OrderBy(s => s.Order).Select(s => new StopResponseDto
            {
                Id = s.Id,
                RouteId = s.RouteId,
                Name = s.Name,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                Order = s.Order
            }).ToList()
        });
    }

    [HttpPost("{routeId:int}/stops/batch")]
    public async Task<IActionResult> AddStopsToRoute(int routeId, [FromBody] List<CreateStopDto> stopsDto)
    {
        var routeExists = await _context.Routes.AnyAsync(r => r.Id == routeId);
        if (!routeExists) return NotFound("Route not found.");

        var newStops = stopsDto.Select(s => new Stop
        {
            RouteId = routeId,
            Name = s.Name,
            Latitude = s.Latitude,
            Longitude = s.Longitude,
            Order = s.Order
        }).ToList();

        _context.Stops.AddRange(newStops);
        await _context.SaveChangesAsync();

        return Ok(newStops);
    }
}