﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsData.Producer.Infrastructure.Data;

#nullable disable

namespace SportsData.Producer.Migrations
{
    [DbContext(typeof(AppDataContext))]
    partial class AppDataContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Franchise", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Abbreviation")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ColorCodeHex")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("DisplayName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DisplayNameShort")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("GlobalId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Nickname")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Slug")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Franchise", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseExternalId", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("FranchiseId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("GlobalId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Provider")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseId");

                    b.ToTable("FranchiseExternalId");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseLogo", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("FranchiseId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("GlobalId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .HasColumnType("nvarchar(max)");

                    b.Property<long>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseId");

                    b.ToTable("FranchiseLogo", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Venue", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("GlobalId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsGrass")
                        .HasColumnType("bit");

                    b.Property<bool>("IsIndoor")
                        .HasColumnType("bit");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ShortName")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Venue", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.VenueExternalId", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid?>("GlobalId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Provider")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("VenueId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("VenueId");

                    b.ToTable("VenueExternalId");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseExternalId", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Franchise", null)
                        .WithMany("ExternalIds")
                        .HasForeignKey("FranchiseId");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseLogo", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Franchise", null)
                        .WithMany("Logos")
                        .HasForeignKey("FranchiseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.VenueExternalId", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Venue", null)
                        .WithMany("ExternalIds")
                        .HasForeignKey("VenueId");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Franchise", b =>
                {
                    b.Navigation("ExternalIds");

                    b.Navigation("Logos");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Venue", b =>
                {
                    b.Navigation("ExternalIds");
                });
#pragma warning restore 612, 618
        }
    }
}
