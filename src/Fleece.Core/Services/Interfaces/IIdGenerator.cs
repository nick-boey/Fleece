namespace Fleece.Core.Services.Interfaces;

public interface IIdGenerator
{
    string Generate(string title);
    string Generate(string title, int salt);
}
