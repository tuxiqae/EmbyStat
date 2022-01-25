﻿// <auto-generated />
using System;
using EmbyStat.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EmbyStat.Migrations.Sqlite
{
    [DbContext(typeof(SqlLiteDbContext))]
    partial class SqlLiteDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "5.0.11");

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlAudioStream", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("BitRate")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ChannelLayout")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Channels")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Codec")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Language")
                        .HasColumnType("TEXT");

                    b.Property<string>("MovieId")
                        .HasColumnType("TEXT");

                    b.Property<int?>("SampleRate")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("AudioStreams");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlGenre", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Genres");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMediaSource", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int?>("BitRate")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Container")
                        .HasColumnType("TEXT");

                    b.Property<string>("MovieId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .HasColumnType("TEXT");

                    b.Property<string>("Protocol")
                        .HasColumnType("TEXT");

                    b.Property<long?>("RunTimeTicks")
                        .HasColumnType("INTEGER");

                    b.Property<double>("SizeInMb")
                        .HasColumnType("REAL");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("MediaSources");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMovie", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<string>("Banner")
                        .HasColumnType("TEXT");

                    b.Property<string>("CollectionId")
                        .HasColumnType("TEXT");

                    b.Property<float?>("CommunityRating")
                        .HasColumnType("REAL");

                    b.Property<string>("Container")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("DateCreated")
                        .HasColumnType("TEXT");

                    b.Property<string>("IMDB")
                        .HasColumnType("TEXT");

                    b.Property<string>("Logo")
                        .HasColumnType("TEXT");

                    b.Property<string>("MediaType")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("OfficialRating")
                        .HasColumnType("TEXT");

                    b.Property<string>("OriginalTitle")
                        .HasColumnType("TEXT");

                    b.Property<string>("ParentId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Path")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("PremiereDate")
                        .HasColumnType("TEXT");

                    b.Property<string>("Primary")
                        .HasColumnType("TEXT");

                    b.Property<int?>("ProductionYear")
                        .HasColumnType("INTEGER");

                    b.Property<long?>("RunTimeTicks")
                        .HasColumnType("INTEGER");

                    b.Property<string>("SortName")
                        .HasColumnType("TEXT");

                    b.Property<int?>("TMDB")
                        .HasColumnType("INTEGER");

                    b.Property<string>("TVDB")
                        .HasColumnType("TEXT");

                    b.Property<string>("Thumb")
                        .HasColumnType("TEXT");

                    b.Property<int>("Video3DFormat")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Movies");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMoviePerson", b =>
                {
                    b.Property<string>("MovieId")
                        .HasColumnType("TEXT");

                    b.Property<string>("PersonId")
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.HasKey("MovieId", "PersonId", "Type");

                    b.HasIndex("PersonId");

                    b.ToTable("SqlMovieSqlPerson");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlPerson", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("TEXT");

                    b.Property<DateTime?>("BirthDate")
                        .HasColumnType("TEXT");

                    b.Property<int?>("MovieCount")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("Primary")
                        .HasColumnType("TEXT");

                    b.Property<int?>("ShowCount")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("People");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlSubtitleStream", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Codec")
                        .HasColumnType("TEXT");

                    b.Property<string>("DisplayTitle")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Language")
                        .HasColumnType("TEXT");

                    b.Property<string>("MovieId")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("SubtitleStreams");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlVideoStream", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AspectRatio")
                        .HasColumnType("TEXT");

                    b.Property<float?>("AverageFrameRate")
                        .HasColumnType("REAL");

                    b.Property<int?>("BitDepth")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("BitRate")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("Channels")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Codec")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsDefault")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Language")
                        .HasColumnType("TEXT");

                    b.Property<string>("MovieId")
                        .HasColumnType("TEXT");

                    b.Property<string>("VideoRange")
                        .HasColumnType("TEXT");

                    b.Property<int?>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MovieId");

                    b.ToTable("VideoStreams");
                });

            modelBuilder.Entity("SqlGenreSqlMovie", b =>
                {
                    b.Property<string>("GenresId")
                        .HasColumnType("TEXT");

                    b.Property<string>("MoviesId")
                        .HasColumnType("TEXT");

                    b.HasKey("GenresId", "MoviesId");

                    b.HasIndex("MoviesId");

                    b.ToTable("SqlGenreSqlMovie");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlAudioStream", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", "Movie")
                        .WithMany("AudioStreams")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.ClientCascade);

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMediaSource", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", "Movie")
                        .WithMany("MediaSources")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.ClientCascade);

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMoviePerson", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", "Movie")
                        .WithMany("MoviePeople")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EmbyStat.Common.SqLite.SqlPerson", "Person")
                        .WithMany("MoviePeople")
                        .HasForeignKey("PersonId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Movie");

                    b.Navigation("Person");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlSubtitleStream", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", "Movie")
                        .WithMany("SubtitleStreams")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.ClientCascade);

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlVideoStream", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", "Movie")
                        .WithMany("VideoStreams")
                        .HasForeignKey("MovieId")
                        .OnDelete(DeleteBehavior.ClientCascade);

                    b.Navigation("Movie");
                });

            modelBuilder.Entity("SqlGenreSqlMovie", b =>
                {
                    b.HasOne("EmbyStat.Common.SqLite.SqlGenre", null)
                        .WithMany()
                        .HasForeignKey("GenresId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("EmbyStat.Common.SqLite.SqlMovie", null)
                        .WithMany()
                        .HasForeignKey("MoviesId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlMovie", b =>
                {
                    b.Navigation("AudioStreams");

                    b.Navigation("MediaSources");

                    b.Navigation("MoviePeople");

                    b.Navigation("SubtitleStreams");

                    b.Navigation("VideoStreams");
                });

            modelBuilder.Entity("EmbyStat.Common.SqLite.SqlPerson", b =>
                {
                    b.Navigation("MoviePeople");
                });
#pragma warning restore 612, 618
        }
    }
}
