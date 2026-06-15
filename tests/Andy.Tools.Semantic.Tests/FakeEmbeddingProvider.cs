using System.Text.RegularExpressions;
using Andy.Tools.Semantic;

namespace Andy.Tools.Semantic.Tests;

/// <summary>
/// A deterministic, network-free embedding provider for tests. It produces a term-frequency vector
/// over a fixed vocabulary, so a chunk that shares more words with the query scores higher under
/// cosine similarity.
/// </summary>
public sealed class FakeEmbeddingProvider : IEmbeddingProvider
{
    private readonly string[] _vocabulary;
    private readonly Dictionary<string, int> _index;

    public FakeEmbeddingProvider(params string[] vocabulary)
    {
        _vocabulary = vocabulary;
        _index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < vocabulary.Length; i++)
        {
            _index[vocabulary[i]] = i;
        }
    }

    public int Dimensions => _vocabulary.Length;

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        var vectors = new List<float[]>(texts.Count);

        foreach (var text in texts)
        {
            var vector = new float[_vocabulary.Length];
            foreach (Match match in Regex.Matches(text, "[A-Za-z]+"))
            {
                if (_index.TryGetValue(match.Value, out var i))
                {
                    vector[i] += 1f;
                }
            }

            vectors.Add(vector);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }
}
