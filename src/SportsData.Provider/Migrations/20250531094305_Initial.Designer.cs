﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SportsData.Provider.Infrastructure.Data;

#nullable disable

namespace SportsData.Provider.Migrations
{
    [DbContext(typeof(AppDataContext))]
    [Migration("20250531094305_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("SportsData.Provider.Infrastructure.Data.Entities.RecurringJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("CronExpression")
                        .HasColumnType("text");

                    b.Property<int>("DocumentType")
                        .HasColumnType("integer");

                    b.Property<string>("Endpoint")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("EndpointMask")
                        .HasColumnType("text");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsRecurring")
                        .HasColumnType("boolean");

                    b.Property<bool>("IsSeasonSpecific")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastAccessed")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("LastPageIndex")
                        .HasColumnType("integer");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Ordinal")
                        .HasColumnType("integer");

                    b.Property<int>("Provider")
                        .HasColumnType("integer");

                    b.Property<int?>("SeasonYear")
                        .HasColumnType("integer");

                    b.Property<int>("SportId")
                        .HasColumnType("integer");

                    b.Property<int?>("TotalPageCount")
                        .HasColumnType("integer");

                    b.Property<string>("UrlHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("Endpoint")
                        .HasDatabaseName("IX_RecurringJob_Endpoint");

                    b.HasIndex("LastAccessed")
                        .HasDatabaseName("IX_RecurringJob_LastAccessed");

                    b.HasIndex("IsEnabled", "Provider", "SportId", "DocumentType", "SeasonYear")
                        .HasDatabaseName("IX_RecurringJob_Enabled_Provider_Sport_DocumentType_Season");

                    b.ToTable("RecurringJob", (string)null);
                });

            modelBuilder.Entity("SportsData.Provider.Infrastructure.Data.Entities.ResourceIndexItem", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("LastAccessed")
                        .HasColumnType("timestamp with time zone");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("OriginalUrlHash")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<Guid>("ResourceIndexId")
                        .HasColumnType("uuid");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("UrlHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("LastAccessed")
                        .HasDatabaseName("IX_ResourceIndexItem_LastAccessed");

                    b.HasIndex("OriginalUrlHash")
                        .HasDatabaseName("IX_ResourceIndexItem_OriginalUrlHash");

                    b.HasIndex("ResourceIndexId", "OriginalUrlHash")
                        .IsUnique()
                        .HasDatabaseName("IX_ResourceIndexItem_Composite");

                    b.ToTable("ResourceIndexItem", (string)null);
                });

            modelBuilder.Entity("SportsData.Provider.Infrastructure.Data.Entities.ScheduledJob", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("DocumentType")
                        .HasColumnType("integer");

                    b.Property<DateTime?>("EndUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("ExecutionMode")
                        .HasColumnType("integer");

                    b.Property<string>("Href")
                        .IsRequired()
                        .HasMaxLength(1024)
                        .HasColumnType("character varying(1024)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("boolean");

                    b.Property<DateTime?>("LastCompletedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("LastEnqueuedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("LastPageIndex")
                        .HasColumnType("integer");

                    b.Property<int?>("MaxAttempts")
                        .HasColumnType("integer");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uuid");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("PollingIntervalInSeconds")
                        .HasColumnType("integer");

                    b.Property<int?>("SeasonYear")
                        .HasColumnType("integer");

                    b.Property<int>("SourceDataProvider")
                        .HasColumnType("integer");

                    b.Property<int>("Sport")
                        .HasColumnType("integer");

                    b.Property<DateTime>("StartUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<DateTime?>("TimeoutAfterUtc")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int?>("TotalPageCount")
                        .HasColumnType("integer");

                    b.Property<string>("UrlHash")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ExecutionMode")
                        .HasDatabaseName("IX_ScheduledJob_ExecutionMode");

                    b.HasIndex("Href")
                        .HasDatabaseName("IX_ScheduledJob_Href");

                    b.HasIndex("IsActive", "StartUtc", "EndUtc")
                        .HasDatabaseName("IX_ScheduledJob_IsActive_StartUtc_EndUtc");

                    b.HasIndex("SourceDataProvider", "Sport", "DocumentType")
                        .HasDatabaseName("IX_ScheduledJob_SourceDataProvider_Sport_DocumentType");

                    b.ToTable("ScheduledJob", (string)null);
                });

            modelBuilder.Entity("SportsData.Provider.Infrastructure.Data.Entities.ResourceIndexItem", b =>
                {
                    b.HasOne("SportsData.Provider.Infrastructure.Data.Entities.RecurringJob", null)
                        .WithMany("Items")
                        .HasForeignKey("ResourceIndexId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Provider.Infrastructure.Data.Entities.RecurringJob", b =>
                {
                    b.Navigation("Items");
                });
#pragma warning restore 612, 618
        }
    }
}
