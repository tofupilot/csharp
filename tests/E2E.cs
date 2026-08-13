using System;
using System.Threading;

namespace TofuPilot.Tests;

/// <summary>
/// Marks every name a test creates as belonging to this CI run.
///
/// The four client suites share one organization, so clients/e2e-cleanup.py
/// deletes by tag rather than by age: untagged entities are never touched,
/// which puts a concurrent job's data — and anything a human seeded — out of
/// reach by construction.
/// </summary>
public static class E2E
{
    public static string Tag { get; } =
        Environment.GetEnvironmentVariable("E2E_TAG") is { Length: > 0 } tag
            ? tag
            // Local runs need a tag of their own: with a counter, a fixed tag
            // would make two runs in a row produce exactly the same names. Ten
            // characters, like CI's, so a fragment costs the same either way.
            : "e2el" + Guid.NewGuid().ToString("N")[..6];

    private static int _counter = -1;

    /// <summary>
    /// A name fragment no other run — and no other name in this run — produces.
    ///
    /// The "s" marks the C# suite; python, Rust and C++ use "p", "r" and "c".
    /// See clients/python-speakeasy/tests/e2e_tag.py for why uniqueness inside
    /// a run is a counter and not a guid, and for the 60-character budget the
    /// four suites share. xUnit runs collections in parallel, hence Interlocked.
    /// </summary>
    public static string Uid() => Tag + "s" + Base36(Interlocked.Increment(ref _counter));

    /// Widens past three characters rather than wrapping: past 46 656 names a
    /// fragment grows, which is visible, instead of repeating one, which is not.
    private static string Base36(int n)
    {
        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var buffer = new char[8];
        var i = buffer.Length;
        while (n > 0)
        {
            buffer[--i] = digits[n % 36];
            n /= 36;
        }
        while (buffer.Length - i < 3) buffer[--i] = '0';
        return new string(buffer, i, buffer.Length - i);
    }
}
