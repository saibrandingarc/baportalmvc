using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Xml;
using Microsoft.EntityFrameworkCore;
using BAPortalMVC.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Define DbSets for your entities
    public DbSet<Settings> Settings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Configure entity properties and relationships here
    }
}
