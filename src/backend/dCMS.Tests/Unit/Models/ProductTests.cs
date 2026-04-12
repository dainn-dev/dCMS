using dCMS.Core.Events;
using dCMS.Core.Exceptions;
using dCMS.Core.Models;
using FluentAssertions;

namespace dCMS.Tests.Unit.Models;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-04-12T10:00:00Z");

    [Fact]
    public void Create_raises_ProductCreated_and_starts_in_Draft()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "my-slug", Now);

        p.Status.Should().Be(ProductStatus.Draft);
        p.Slug.Should().Be("my-slug");
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductCreated>();
    }

    [Fact]
    public void Publish_from_draft_sets_Active_and_raises_ProductPublished()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.ClearDomainEvents();

        p.Publish(Now.AddMinutes(1));

        p.Status.Should().Be(ProductStatus.Active);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductPublished>();
    }

    [Fact]
    public void Publish_when_archived_throws()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.Archive(Now.AddMinutes(1));
        p.ClearDomainEvents();

        var act = () => p.Publish(Now.AddMinutes(2));

        act.Should().Throw<InvalidProductStateException>().WithMessage("*archived*");
    }

    [Fact]
    public void Publish_when_already_active_is_idempotent()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.Publish(Now.AddMinutes(1));
        p.ClearDomainEvents();

        p.Publish(Now.AddMinutes(2));

        p.Status.Should().Be(ProductStatus.Active);
        p.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Archive_raises_ProductArchived()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.ClearDomainEvents();

        p.Archive(Now.AddMinutes(1));

        p.Status.Should().Be(ProductStatus.Archived);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductArchived>();
    }

    [Fact]
    public void SubmitForApproval_transitions_Draft_to_Pending_and_raises_ProductUpdated()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.ClearDomainEvents();

        p.SubmitForApproval(Now.AddMinutes(1));

        p.Status.Should().Be(ProductStatus.PendingApproval);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductUpdated>();
    }

    [Fact]
    public void UpdateDetails_raises_ProductUpdated_when_fields_change()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"A"}""", "{}", "slug", Now);
        p.ClearDomainEvents();

        p.UpdateDetails(2, """{"vi":"B"}""", "{}", "slug", Now.AddMinutes(1));

        p.CategoryId.Should().Be(2);
        p.NameJson.Should().Contain("B");
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductUpdated>();
    }

    [Fact]
    public void Hide_moves_active_to_hidden_and_raises_ProductUpdated()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.Publish(Now.AddMinutes(1));
        p.ClearDomainEvents();

        p.Hide(Now.AddMinutes(2));

        p.Status.Should().Be(ProductStatus.Hidden);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductUpdated>();
    }

    [Fact]
    public void Unhide_moves_hidden_to_active_and_raises_ProductPublished()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"Áo"}""", "{}", "slug", Now);
        p.Publish(Now.AddMinutes(1));
        p.Hide(Now.AddMinutes(2));
        p.ClearDomainEvents();

        p.Unhide(Now.AddMinutes(3));

        p.Status.Should().Be(ProductStatus.Active);
        p.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductPublished>();
    }

    [Fact]
    public void RecordVariantsGenerated_throws_when_archived()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"A"}""", "{}", "slug", Now);
        p.Archive(Now.AddMinutes(1));
        p.ClearDomainEvents();

        var act = () => p.RecordVariantsGenerated(Now.AddMinutes(2));

        act.Should().Throw<InvalidProductStateException>().WithMessage("*archived*");
    }

    [Fact]
    public void UpdateDetails_throws_when_archived()
    {
        var p = Product.Create("t1", "s1", 1, """{"vi":"A"}""", "{}", "slug", Now);
        p.Archive(Now.AddMinutes(1));
        p.ClearDomainEvents();

        var act = () => p.UpdateDetails(2, """{"vi":"B"}""", "{}", "slug", Now.AddMinutes(2));

        act.Should().Throw<InvalidProductStateException>().WithMessage("*archived*");
    }
}
