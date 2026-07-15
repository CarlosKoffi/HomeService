namespace HomeService.Contracts.Cms;

public sealed record CompanyHomeCmsResponse(
    CompanyHomeHeroCmsResponse Hero,
    CompanyHomeStepsCmsResponse Steps,
    CompanyHomeTrustedCmsResponse Trusted,
    CompanyHomeDashboardCmsResponse Dashboard,
    CompanyHomeFaqCmsResponse Faq,
    CompanyHomeContactCmsResponse Contact,
    CompanyHomeFooterCmsResponse Footer);

public sealed record CompanyHomeHeroCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    CmsLinkResponse PrimaryCta,
    CmsLinkResponse SecondaryCta,
    string ImageUrl,
    string ImageAlt,
    IReadOnlyList<string> ProofItems);

public sealed record CompanyHomeStepsCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    IReadOnlyList<CmsStepResponse> Items);

public sealed record CompanyHomeTrustedCmsResponse(
    string Headline,
    IReadOnlyList<string> Items);

public sealed record CompanyHomeDashboardCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    IReadOnlyList<CmsDashboardStatResponse> Stats,
    IReadOnlyList<string> Requests,
    IReadOnlyList<string> Providers);

public sealed record CompanyHomeFaqCmsResponse(
    string Label,
    string Headline,
    IReadOnlyList<CmsFaqItemResponse> Questions);

public sealed record CompanyHomeContactCmsResponse(
    string Label,
    string Headline,
    string Subtitle,
    IReadOnlyList<string> Tags);

public sealed record CompanyHomeFooterCmsResponse(
    string BrandText,
    string Copyright,
    string Baseline,
    IReadOnlyList<CmsFooterColumnResponse> Columns);

public sealed record CmsLinkResponse(string Label, string Url);

public sealed record CmsStepResponse(
    string Number,
    string Label,
    string Title,
    string Text,
    string Image);

public sealed record CmsDashboardStatResponse(
    string Label,
    string Value,
    string Help);

public sealed record CmsFaqItemResponse(
    string Question,
    string Answer);

public sealed record CmsFooterColumnResponse(
    string Title,
    IReadOnlyList<string> Links);
