using Fleece.Core.Models;
using Fleece.Core.Services;
using FluentAssertions;
using NUnit.Framework;

namespace Fleece.Core.Tests.Services;

[TestFixture]
public class TagServiceTests
{
    private TagService _sut = null!;

    [SetUp]
    public void SetUp()
    {
        _sut = new TagService();
    }

    #region ValidateTag

    [Test]
    public void ValidateTag_ReturnsNull_ForSimpleTag()
    {
        var result = _sut.ValidateTag("backend");

        result.Should().BeNull();
    }

    [Test]
    public void ValidateTag_ReturnsNull_ForKeyValueTag()
    {
        var result = _sut.ValidateTag("project=frontend");

        result.Should().BeNull();
    }

    [Test]
    public void ValidateTag_ReturnsError_ForTagWithSpaces()
    {
        var result = _sut.ValidateTag("bad tag");

        result.Should().NotBeNull();
        result.Should().Contain("whitespace");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForMultipleEquals()
    {
        var result = _sut.ValidateTag("key=value=extra");

        result.Should().NotBeNull();
        result.Should().Contain("multiple '='");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForEmptyKey()
    {
        var result = _sut.ValidateTag("=value");

        result.Should().NotBeNull();
        result.Should().Contain("empty key");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForEmptyValue()
    {
        var result = _sut.ValidateTag("key=");

        result.Should().NotBeNull();
        result.Should().Contain("empty value");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForReservedKey()
    {
        var result = _sut.ValidateTag("status=open");

        result.Should().NotBeNull();
        result.Should().Contain("reserved");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForReservedKey_CaseInsensitive()
    {
        var result = _sut.ValidateTag("STATUS=open");

        result.Should().NotBeNull();
        result.Should().Contain("reserved");
    }

    [TestCase("id")]
    [TestCase("title")]
    [TestCase("description")]
    [TestCase("type")]
    [TestCase("priority")]
    [TestCase("linkedpr")]
    [TestCase("linkedissues")]
    [TestCase("parentissues")]
    [TestCase("assignedto")]
    [TestCase("tags")]
    [TestCase("workingbranchid")]
    [TestCase("executionmode")]
    [TestCase("createdby")]
    [TestCase("createdat")]
    [TestCase("lastupdate")]
    [TestCase("questions")]
    public void ValidateTag_ReturnsError_ForAllReservedKeys(string key)
    {
        var result = _sut.ValidateTag($"{key}=somevalue");

        result.Should().NotBeNull();
        result.Should().Contain("reserved");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForEmptyTag()
    {
        var result = _sut.ValidateTag("");

        result.Should().NotBeNull();
        result.Should().Contain("empty");
    }

    [Test]
    public void ValidateTag_ReturnsError_ForWhitespaceOnlyTag()
    {
        var result = _sut.ValidateTag("   ");

        result.Should().NotBeNull();
        result.Should().Contain("empty");
    }

    #endregion

    #region ValidateTags

    [Test]
    public void ValidateTags_ReturnsEmpty_ForValidTags()
    {
        var result = _sut.ValidateTags(["backend", "project=frontend", "urgent"]);

        result.Should().BeEmpty();
    }

    [Test]
    public void ValidateTags_ReturnsEmpty_ForNullTags()
    {
        var result = _sut.ValidateTags(null);

        result.Should().BeEmpty();
    }

    [Test]
    public void ValidateTags_ReturnsEmpty_ForEmptyList()
    {
        var result = _sut.ValidateTags([]);

        result.Should().BeEmpty();
    }

    [Test]
    public void ValidateTags_ReturnsErrors_ForInvalidTags()
    {
        var result = _sut.ValidateTags(["valid", "bad tag", "status=open"]);

        result.Should().HaveCount(2);
    }

    #endregion

    #region ParseTag

    [Test]
    public void ParseTag_ReturnsKeyOnly_ForSimpleTag()
    {
        var (key, value) = _sut.ParseTag("backend");

        key.Should().Be("backend");
        value.Should().BeNull();
    }

    [Test]
    public void ParseTag_ReturnsKeyAndValue_ForKeyValueTag()
    {
        var (key, value) = _sut.ParseTag("project=frontend");

        key.Should().Be("project");
        value.Should().Be("frontend");
    }

    [Test]
    public void ParseTag_HandlesEmptyString()
    {
        var (key, value) = _sut.ParseTag("");

        key.Should().Be(string.Empty);
        value.Should().BeNull();
    }

    [Test]
    public void ParseTag_HandlesEqualsAtStart()
    {
        var (key, value) = _sut.ParseTag("=value");

        key.Should().Be(string.Empty);
        value.Should().Be("value");
    }

    [Test]
    public void ParseTag_HandlesEqualsAtEnd()
    {
        var (key, value) = _sut.ParseTag("key=");

        key.Should().Be("key");
        value.Should().Be(string.Empty);
    }

    #endregion

    #region HasKeyedTag

    [Test]
    public void HasKeyedTag_ReturnsTrue_WhenKeyAndValueMatch()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project=frontend", "backend"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeTrue();
    }

    [Test]
    public void HasKeyedTag_ReturnsFalse_WhenKeyDoesNotExist()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["backend"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeFalse();
    }

    [Test]
    public void HasKeyedTag_ReturnsFalse_WhenValueDoesNotMatch()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project=backend"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeFalse();
    }

    [Test]
    public void HasKeyedTag_IsCaseInsensitive_ForKey()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["PROJECT=frontend"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeTrue();
    }

    [Test]
    public void HasKeyedTag_IsCaseInsensitive_ForValue()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project=FRONTEND"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeTrue();
    }

    [Test]
    public void HasKeyedTag_ReturnsFalse_WhenTagsIsNull()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = null!,
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeFalse();
    }

    [Test]
    public void HasKeyedTag_ReturnsFalse_WhenTagsIsEmpty()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = [],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeFalse();
    }

    [Test]
    public void HasKeyedTag_ReturnsFalse_ForSimpleTagMatchingKey()
    {
        // A simple tag "project" should NOT match when searching for key "project"
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.HasKeyedTag(issue, "project", "frontend");

        result.Should().BeFalse();
    }

    #endregion

    #region GetKeyedTags

    [Test]
    public void GetKeyedTags_ReturnsOnlyKeyedTags()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project=frontend", "backend", "priority=high"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.GetKeyedTags(issue);

        result.Should().HaveCount(2);
        result.Should().ContainKey("project");
        result.Should().ContainKey("priority");
    }

    [Test]
    public void GetKeyedTags_GroupsMultipleValuesUnderSameKey()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["project=frontend", "project=services"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.GetKeyedTags(issue);

        result.Should().HaveCount(1);
        result["project"].Should().HaveCount(2);
        result["project"].Should().Contain("frontend");
        result["project"].Should().Contain("services");
    }

    [Test]
    public void GetKeyedTags_ReturnsEmpty_WhenNoKeyedTags()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["backend", "api"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.GetKeyedTags(issue);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetKeyedTags_ReturnsEmpty_WhenTagsIsNull()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = null!,
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.GetKeyedTags(issue);

        result.Should().BeEmpty();
    }

    [Test]
    public void GetKeyedTags_IsCaseInsensitiveForKeys()
    {
        var issue = new Issue
        {
            Id = "test",
            Title = "Test",
            Status = IssueStatus.Open,
            Type = IssueType.Task,
            Tags = ["Project=frontend", "PROJECT=services"],
            LastUpdate = DateTimeOffset.UtcNow
        };

        var result = _sut.GetKeyedTags(issue);

        // Should have one key with two values
        result.Should().HaveCount(1);
        result.Values.First().Should().HaveCount(2);
    }

    #endregion
}
