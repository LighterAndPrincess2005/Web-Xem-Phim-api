using System.Text.Json.Serialization;

namespace LVDKMovie.Models;

// ── Home response ──────────────────────────────────────────────
public class HomeResponse
{
    [JsonPropertyName("status")] public bool Status { get; set; }
    [JsonPropertyName("items")] public List<MovieItem> Items { get; set; } = new();
}

// ── Movie item (card) ──────────────────────────────────────────
public class MovieItem
{
    [JsonPropertyName("_id")]       public string Id       { get; set; } = "";
    [JsonPropertyName("name")]      public string Name     { get; set; } = "";
    [JsonPropertyName("slug")]      public string Slug     { get; set; } = "";
    [JsonPropertyName("origin_name")] public string OriginName { get; set; } = "";
    [JsonPropertyName("year")]      public int Year        { get; set; }
    [JsonPropertyName("thumb_url")] public string ThumbUrl { get; set; } = "";
    [JsonPropertyName("poster_url")] public string PosterUrl { get; set; } = "";
    [JsonPropertyName("episode_current")] public string EpisodeCurrent { get; set; } = "";
    [JsonPropertyName("type")]      public string Type     { get; set; } = "";
    [JsonPropertyName("lang")]      public string Lang     { get; set; } = "";
    [JsonPropertyName("quality")]   public string Quality  { get; set; } = "";
}

// ── Movie detail ───────────────────────────────────────────────
public class MovieDetailResponse
{
    [JsonPropertyName("status")]   public bool Status  { get; set; }
    [JsonPropertyName("movie")]    public MovieDetail? Movie    { get; set; }
    [JsonPropertyName("episodes")] public List<ServerGroup> Episodes { get; set; } = new();
}

public class MovieDetail
{
    [JsonPropertyName("_id")]          public string Id          { get; set; } = "";
    [JsonPropertyName("name")]         public string Name        { get; set; } = "";
    [JsonPropertyName("slug")]         public string Slug        { get; set; } = "";
    [JsonPropertyName("origin_name")]  public string OriginName  { get; set; } = "";
    [JsonPropertyName("content")]      public string Content     { get; set; } = "";
    [JsonPropertyName("type")]         public string Type        { get; set; } = "";
    [JsonPropertyName("status")]       public string Status      { get; set; } = "";
    [JsonPropertyName("thumb_url")]    public string ThumbUrl    { get; set; } = "";
    [JsonPropertyName("poster_url")]   public string PosterUrl   { get; set; } = "";
    [JsonPropertyName("year")]         public int Year           { get; set; }
    [JsonPropertyName("quality")]      public string Quality     { get; set; } = "";
    [JsonPropertyName("lang")]         public string Lang        { get; set; } = "";
    [JsonPropertyName("time")]         public string Time        { get; set; } = "";
    [JsonPropertyName("episode_current")] public string EpisodeCurrent { get; set; } = "";
    [JsonPropertyName("episode_total")]   public string EpisodeTotal   { get; set; } = "";
    [JsonPropertyName("category")]     public List<CategoryItem> Category { get; set; } = new();
    [JsonPropertyName("country")]      public List<CategoryItem> Country  { get; set; } = new();
    [JsonPropertyName("director")]     public List<string> Director  { get; set; } = new();
    [JsonPropertyName("actor")]        public List<string> Actor     { get; set; } = new();
}

public class CategoryItem
{
    [JsonPropertyName("id")]   public string Id   { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
}

// ── Episodes ───────────────────────────────────────────────────
public class ServerGroup
{
    [JsonPropertyName("server_name")] public string ServerName { get; set; } = "";
    [JsonPropertyName("server_data")] public List<EpisodeItem> ServerData { get; set; } = new();
}

public class EpisodeItem
{
    [JsonPropertyName("name")]     public string Name    { get; set; } = "";
    [JsonPropertyName("slug")]     public string Slug    { get; set; } = "";
    [JsonPropertyName("filename")] public string Filename { get; set; } = "";
    [JsonPropertyName("link_embed")] public string LinkEmbed { get; set; } = "";
    [JsonPropertyName("link_m3u8")]  public string LinkM3u8  { get; set; } = "";
    [JsonPropertyName("link_sub")] public string LinkSub { get; set; } = "";
    [JsonPropertyName("subtitle_url")] public string SubtitleUrl { get; set; } = "";
    [JsonPropertyName("subtitles")] public List<SubtitleItem> Subtitles { get; set; } = new();
}

public class SubtitleItem
{
    [JsonPropertyName("lang")] public string Lang { get; set; } = "";
    [JsonPropertyName("language")] public string Language { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("file")] public string File { get; set; } = "";
    [JsonPropertyName("link")] public string Link { get; set; } = "";
}

// ── Search / List ──────────────────────────────────────────────
public class ListResponse
{
    [JsonPropertyName("status")]   public bool Status { get; set; }
    [JsonPropertyName("items")]    public List<MovieItem> Items { get; set; } = new();
    [JsonPropertyName("paginate")] public Paginate? Paginate { get; set; }
}

public class Paginate
{
    [JsonPropertyName("totalItems")]   public int TotalItems   { get; set; }
    [JsonPropertyName("totalPages")]   public int TotalPages   { get; set; }
    [JsonPropertyName("currentPage")]  public int CurrentPage  { get; set; }
}

// ── Watch ViewModel ────────────────────────────────────────────
public class WatchViewModel
{
    public MovieDetail Movie    { get; set; } = new();
    public List<ServerGroup> Episodes { get; set; } = new();
    public string CurrentServer { get; set; } = "";
    public string CurrentEpisode { get; set; } = "";
    public string EmbedUrl      { get; set; } = "";
    public string M3u8Url       { get; set; } = "";
    public string SubtitleUrl   { get; set; } = "";
}
