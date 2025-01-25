﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SportsData.Producer.Infrastructure.Data;

#nullable disable

namespace SportsData.Producer.Migrations
{
    [DbContext(typeof(AppDataContext))]
    [Migration("20250125145916_ILogoChanges")]
    partial class ILogoChanges
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("MassTransit.EntityFrameworkCoreIntegration.InboxState", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("Id"));

                    b.Property<DateTime?>("Consumed")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("ConsumerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("Delivered")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("ExpirationTime")
                        .HasColumnType("datetime2");

                    b.Property<long?>("LastSequenceNumber")
                        .HasColumnType("bigint");

                    b.Property<Guid>("LockId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MessageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("ReceiveCount")
                        .HasColumnType("int");

                    b.Property<DateTime>("Received")
                        .HasColumnType("datetime2");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.HasKey("Id");

                    b.HasIndex("Delivered");

                    b.ToTable("InboxState");
                });

            modelBuilder.Entity("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage", b =>
                {
                    b.Property<long>("SequenceNumber")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<long>("SequenceNumber"));

                    b.Property<string>("Body")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContentType")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<Guid?>("ConversationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("CorrelationId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("DestinationAddress")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<DateTime?>("EnqueueTime")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("ExpirationTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("FaultAddress")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<string>("Headers")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("InboxConsumerId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("InboxMessageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("InitiatorId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("MessageId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("MessageType")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("OutboxId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Properties")
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("RequestId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ResponseAddress")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.Property<DateTime>("SentTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("SourceAddress")
                        .HasMaxLength(256)
                        .HasColumnType("nvarchar(256)");

                    b.HasKey("SequenceNumber");

                    b.HasIndex("EnqueueTime");

                    b.HasIndex("ExpirationTime");

                    b.HasIndex("OutboxId", "SequenceNumber")
                        .IsUnique()
                        .HasFilter("[OutboxId] IS NOT NULL");

                    b.HasIndex("InboxMessageId", "InboxConsumerId", "SequenceNumber")
                        .IsUnique()
                        .HasFilter("[InboxMessageId] IS NOT NULL AND [InboxConsumerId] IS NOT NULL");

                    b.ToTable("OutboxMessage");
                });

            modelBuilder.Entity("MassTransit.EntityFrameworkCoreIntegration.OutboxState", b =>
                {
                    b.Property<Guid>("OutboxId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("Created")
                        .HasColumnType("datetime2");

                    b.Property<DateTime?>("Delivered")
                        .HasColumnType("datetime2");

                    b.Property<long?>("LastSequenceNumber")
                        .HasColumnType("bigint");

                    b.Property<Guid>("LockId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<byte[]>("RowVersion")
                        .IsConcurrencyToken()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("rowversion");

                    b.HasKey("OutboxId");

                    b.HasIndex("Created");

                    b.ToTable("OutboxState");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Franchise", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Abbreviation")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ColorCodeAltHex")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ColorCodeHex")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DisplayNameShort")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Nickname")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Sport")
                        .HasColumnType("int");

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

                    b.Property<Guid>("FranchiseId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Provider")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseId");

                    b.ToTable("FranchiseExternalId", (string)null);
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

                    b.Property<long?>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("OriginalUrlHash")
                        .HasColumnType("int");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseId");

                    b.ToTable("FranchiseLogo", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeason", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Abbreviation")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ColorCodeAltHex")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ColorCodeHex")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("DisplayName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("DisplayNameShort")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("FranchiseId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<bool>("IsActive")
                        .HasColumnType("bit");

                    b.Property<bool>("IsAllStar")
                        .HasColumnType("bit");

                    b.Property<string>("Location")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Losses")
                        .HasColumnType("int");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Season")
                        .HasColumnType("int");

                    b.Property<string>("Slug")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Ties")
                        .HasColumnType("int");

                    b.Property<int>("Wins")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseId");

                    b.ToTable("FranchiseSeason", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeasonLogo", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("FranchiseSeasonId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long?>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("OriginalUrlHash")
                        .HasColumnType("int");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("FranchiseSeasonId");

                    b.ToTable("FranchiseSeasonLogo", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Group", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("Abbreviation")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsConference")
                        .HasColumnType("bit");

                    b.Property<string>("MidsizeName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid?>("ParentGroupId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<string>("ShortName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("Group", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupExternalId", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("GroupId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Provider")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.ToTable("GroupExternalId", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupLogo", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("GroupId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long?>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("OriginalUrlHash")
                        .HasColumnType("int");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.ToTable("GroupLogo", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupSeason", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("GroupId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Season")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("GroupId");

                    b.ToTable("GroupSeason", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupSeasonLogo", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<Guid>("GroupSeasonId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long?>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("OriginalUrlHash")
                        .HasColumnType("int");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<long?>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("GroupSeasonId");

                    b.ToTable("GroupSeasonLogo", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Venue", b =>
                {
                    b.Property<Guid>("Id")
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsGrass")
                        .HasColumnType("bit");

                    b.Property<bool>("IsIndoor")
                        .HasColumnType("bit");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ShortName")
                        .IsRequired()
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

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("Provider")
                        .HasColumnType("int");

                    b.Property<string>("Value")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("VenueId")
                        .HasColumnType("uniqueidentifier");

                    b.HasKey("Id");

                    b.HasIndex("VenueId");

                    b.ToTable("VenueExternalId", (string)null);
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.VenueImage", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uniqueidentifier");

                    b.Property<Guid>("CreatedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime>("CreatedUtc")
                        .HasColumnType("datetime2");

                    b.Property<long?>("Height")
                        .HasColumnType("bigint");

                    b.Property<Guid?>("ModifiedBy")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("ModifiedUtc")
                        .HasColumnType("datetime2");

                    b.Property<int>("OriginalUrlHash")
                        .HasColumnType("int");

                    b.PrimitiveCollection<string>("Rel")
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<Guid>("VenueId")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long?>("Width")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("OriginalUrlHash");

                    b.HasIndex("VenueId");

                    b.ToTable("VenueImage", (string)null);
                });

            modelBuilder.Entity("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage", b =>
                {
                    b.HasOne("MassTransit.EntityFrameworkCoreIntegration.OutboxState", null)
                        .WithMany()
                        .HasForeignKey("OutboxId");

                    b.HasOne("MassTransit.EntityFrameworkCoreIntegration.InboxState", null)
                        .WithMany()
                        .HasForeignKey("InboxMessageId", "InboxConsumerId")
                        .HasPrincipalKey("MessageId", "ConsumerId");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseExternalId", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Franchise", "Franchise")
                        .WithMany("ExternalIds")
                        .HasForeignKey("FranchiseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Franchise");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseLogo", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Franchise", null)
                        .WithMany("Logos")
                        .HasForeignKey("FranchiseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeason", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Franchise", null)
                        .WithMany("Seasons")
                        .HasForeignKey("FranchiseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeasonLogo", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeason", null)
                        .WithMany("Logos")
                        .HasForeignKey("FranchiseSeasonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupExternalId", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Group", "Group")
                        .WithMany("ExternalIds")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Group");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupLogo", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Group", null)
                        .WithMany("Logos")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupSeason", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Group", null)
                        .WithMany("Seasons")
                        .HasForeignKey("GroupId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupSeasonLogo", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.GroupSeason", null)
                        .WithMany("Logos")
                        .HasForeignKey("GroupSeasonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.VenueExternalId", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Venue", "Venue")
                        .WithMany("ExternalIds")
                        .HasForeignKey("VenueId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Venue");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.VenueImage", b =>
                {
                    b.HasOne("SportsData.Producer.Infrastructure.Data.Entities.Venue", null)
                        .WithMany("Images")
                        .HasForeignKey("VenueId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Franchise", b =>
                {
                    b.Navigation("ExternalIds");

                    b.Navigation("Logos");

                    b.Navigation("Seasons");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.FranchiseSeason", b =>
                {
                    b.Navigation("Logos");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Group", b =>
                {
                    b.Navigation("ExternalIds");

                    b.Navigation("Logos");

                    b.Navigation("Seasons");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.GroupSeason", b =>
                {
                    b.Navigation("Logos");
                });

            modelBuilder.Entity("SportsData.Producer.Infrastructure.Data.Entities.Venue", b =>
                {
                    b.Navigation("ExternalIds");

                    b.Navigation("Images");
                });
#pragma warning restore 612, 618
        }
    }
}
