#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Datra.Serializers
{
    /// <summary>
    /// Keeps the comments and layout of a hand-written YAML file when Datra saves it.
    ///
    /// Datra serializes a table by rebuilding the whole document from objects, and
    /// YamlDotNet's object model has nowhere to keep comments, so a save used to throw
    /// away every <c>#</c> line a designer wrote. This class does not try to make the
    /// object model carry comments. It diffs two canonical renderings instead:
    /// <list type="bullet">
    ///   <item><c>originalCanonical</c>: what the serializer writes for the data as it is in the file now,</item>
    ///   <item><c>updatedCanonical</c>: what the serializer writes for the data being saved,</item>
    /// </list>
    /// and replays only the differences onto the original text. Rows and fields whose
    /// canonical form did not change are copied from the original byte for byte, so
    /// comments, blank lines, quoting and flow lists (<c>[1, 2]</c>) all survive.
    ///
    /// Comment ownership, which decides what happens on delete and reorder:
    /// <list type="bullet">
    ///   <item>comment lines directly above a row or field (no blank line in between) belong to it,</item>
    ///   <item>a comment at the end of a line belongs to that line,</item>
    ///   <item>a comment block separated from the next row by a blank line is a section comment and stays where it is,</item>
    ///   <item>comments before the first row's own comments are the file header and stay at the top.</item>
    /// </list>
    ///
    /// Anything this cannot patch safely (anchors, aliases, multi-document files, a
    /// structure it does not recognise) makes <see cref="Merge"/> return null, and
    /// <see cref="Reconcile{T}"/> then writes the plain canonical text, which is exactly
    /// what Datra wrote before this class existed. <see cref="Reconcile{T}"/> also
    /// re-reads its own output and falls back the same way if the data does not come
    /// back identical, so a bug here can lose comments but never data.
    /// </summary>
    public static class YamlCommentPreserver
    {
        /// <summary>
        /// Returns the text to write for <paramref name="updatedText"/>, carrying over the
        /// comments and layout of <paramref name="originalText"/>.
        /// </summary>
        /// <param name="originalText">The file as it is on disk now. Null or blank: nothing to preserve.</param>
        /// <param name="updatedText">The canonical text the serializer produced for the data being saved.</param>
        /// <param name="deserialize">The same deserializer the repository loads the file with.</param>
        /// <param name="serialize">The same serializer that produced <paramref name="updatedText"/>.</param>
        public static string Reconcile<T>(
            string? originalText,
            string updatedText,
            Func<string, T> deserialize,
            Func<T, string> serialize)
        {
            if (string.IsNullOrWhiteSpace(originalText))
                return updatedText;

            try
            {
                var originalCanonical = serialize(deserialize(originalText!));

                // Nothing changed: keep the file exactly as it is.
                if (originalCanonical == updatedText)
                    return originalText!;

                var merged = Merge(originalText!, originalCanonical, updatedText);
                if (merged == null)
                    return updatedText;

                // The merged text must load as exactly what the plain text would load as.
                // (Compared after a reload on both sides: a value the serializer omits, such
                // as a null with a non-null default, does not survive a reload either way.)
                var mergedReloaded = serialize(deserialize(merged));
                if (mergedReloaded != updatedText && mergedReloaded != serialize(deserialize(updatedText)))
                    return updatedText;

                return merged;
            }
            catch (Exception)
            {
                return updatedText;
            }
        }

        /// <summary>
        /// Replays the difference between two canonical renderings onto the original text.
        /// Returns null when the original cannot be patched safely.
        /// </summary>
        /// <param name="originalText">The hand-written file, with comments.</param>
        /// <param name="originalCanonical">Canonical rendering of the data in <paramref name="originalText"/>.</param>
        /// <param name="updatedCanonical">Canonical rendering of the data to save.</param>
        public static string? Merge(string originalText, string originalCanonical, string updatedCanonical)
        {
            if (originalText == null) throw new ArgumentNullException(nameof(originalText));
            if (originalCanonical == null) throw new ArgumentNullException(nameof(originalCanonical));
            if (updatedCanonical == null) throw new ArgumentNullException(nameof(updatedCanonical));

            var crlf = originalText.Contains("\r\n");
            var o = originalText.Replace("\r\n", "\n");
            var r0Text = originalCanonical.Replace("\r\n", "\n");
            var r1Text = updatedCanonical.Replace("\r\n", "\n");

            Node? oRoot, r0Root, r1Root;
            try
            {
                oRoot = Node.ParseDocument(o);
                r0Root = Node.ParseDocument(r0Text);
                r1Root = Node.ParseDocument(r1Text);
            }
            catch (YamlException)
            {
                return null;
            }

            if (oRoot == null || r0Root == null || r1Root == null)
                return null;

            var merger = new Merger(o, r1Text);
            string? result;

            if (oRoot.Kind == NodeKind.Sequence && !oRoot.Flow)
            {
                if (r0Root.Kind != NodeKind.Sequence || r1Root.Kind != NodeKind.Sequence)
                    return null;
                result = merger.MergeSequence(oRoot, r0Root, r1Root, root: true);
            }
            else
            {
                var replacement = merger.Render(oRoot, r0Root, r1Root, 0, 0, root: true);
                result = replacement == null
                    ? null
                    : o.Substring(0, oRoot.Start) + replacement + o.Substring(oRoot.SpanEnd);
            }

            if (result == null)
                return null;

            return crlf ? result.Replace("\n", "\r\n") : result;
        }

        #region Node model

        private enum NodeKind
        {
            Scalar,
            Mapping,
            Sequence,
            Alias
        }

        /// <summary>
        /// A YAML node with its position in the source text. YamlDotNet's representation
        /// model resolves aliases and loses where a block collection really ends, so this
        /// is built straight from parser events.
        /// </summary>
        private sealed class Node
        {
            public NodeKind Kind;
            public bool Flow;
            public int Start;

            /// <summary>End of the last character that belongs to the node's content.</summary>
            public int ContentEnd;

            /// <summary>
            /// End of the region a rendering of this node replaces. For a block collection
            /// this runs to the end of its last line, so a trailing comment on that line
            /// is part of the collection; for anything else it is <see cref="ContentEnd"/>.
            /// </summary>
            public int SpanEnd;

            public string Value = string.Empty;
            public ScalarStyle Style;
            public string Tag = string.Empty;
            public bool HasAnchor;

            public readonly List<Node> Items = new List<Node>();
            public readonly List<Node> Keys = new List<Node>();
            public readonly List<Node> Values = new List<Node>();

            /// <summary>An implicit null (<c>Key:</c> or <c>- </c> with nothing after it).</summary>
            public bool IsEmpty => Kind == NodeKind.Scalar && Style == ScalarStyle.Plain && Value.Length == 0 && ContentEnd == Start;

            public bool IsBlockCollection => (Kind == NodeKind.Mapping || Kind == NodeKind.Sequence) && !Flow;

            public static Node? ParseDocument(string text)
            {
                var parser = new Parser(new StringReader(text));
                parser.Consume<StreamStart>();
                if (parser.Accept<StreamEnd>(out _))
                    return null;

                parser.Consume<DocumentStart>();
                var root = Read(parser, text);
                parser.Consume<DocumentEnd>();

                // More than one document: not something a data file should be.
                if (!parser.Accept<StreamEnd>(out _))
                    return null;

                return root;
            }

            private static Node Read(IParser parser, string text)
            {
                var ev = parser.Current;
                parser.MoveNext();

                switch (ev)
                {
                    case Scalar scalar:
                    {
                        var node = new Node
                        {
                            Kind = NodeKind.Scalar,
                            Start = (int)scalar.Start.Index,
                            Value = scalar.Value,
                            Style = scalar.Style,
                            Tag = scalar.Tag.IsEmpty ? string.Empty : scalar.Tag.Value,
                            HasAnchor = !scalar.Anchor.IsEmpty,
                        };
                        var end = (int)scalar.End.Index;
                        // Block scalars (| and >) report an end past their trailing line breaks.
                        while (end > node.Start && char.IsWhiteSpace(text[end - 1]))
                            end--;
                        node.ContentEnd = end;
                        node.SpanEnd = end;
                        return node;
                    }

                    case AnchorAlias alias:
                        return new Node
                        {
                            Kind = NodeKind.Alias,
                            Start = (int)alias.Start.Index,
                            ContentEnd = (int)alias.End.Index,
                            SpanEnd = (int)alias.End.Index,
                        };

                    case SequenceStart sequenceStart:
                    {
                        var node = new Node
                        {
                            Kind = NodeKind.Sequence,
                            Flow = sequenceStart.Style == SequenceStyle.Flow,
                            Start = (int)sequenceStart.Start.Index,
                            Tag = sequenceStart.Tag.IsEmpty ? string.Empty : sequenceStart.Tag.Value,
                            HasAnchor = !sequenceStart.Anchor.IsEmpty,
                        };
                        while (!(parser.Current is SequenceEnd))
                            node.Items.Add(Read(parser, text));
                        var sequenceEnd = parser.Current;
                        parser.MoveNext();
                        node.FinishCollection(text, sequenceEnd!);
                        return node;
                    }

                    case MappingStart mappingStart:
                    {
                        var node = new Node
                        {
                            Kind = NodeKind.Mapping,
                            Flow = mappingStart.Style == MappingStyle.Flow,
                            Start = (int)mappingStart.Start.Index,
                            Tag = mappingStart.Tag.IsEmpty ? string.Empty : mappingStart.Tag.Value,
                            HasAnchor = !mappingStart.Anchor.IsEmpty,
                        };
                        while (!(parser.Current is MappingEnd))
                        {
                            node.Keys.Add(Read(parser, text));
                            node.Values.Add(Read(parser, text));
                        }
                        var mappingEnd = parser.Current;
                        parser.MoveNext();
                        node.FinishCollection(text, mappingEnd!);
                        return node;
                    }

                    default:
                        throw new YamlException($"Unexpected YAML event {ev?.GetType().Name}.");
                }
            }

            private void FinishCollection(string text, ParsingEvent endEvent)
            {
                if (Flow)
                {
                    // The end event of a flow collection starts at its closing bracket.
                    var bracket = (int)endEvent.Start.Index;
                    if (bracket >= text.Length || (text[bracket] != ']' && text[bracket] != '}'))
                        throw new YamlException("Could not locate the end of a flow collection.");
                    ContentEnd = bracket + 1;
                    SpanEnd = bracket + 1;
                    return;
                }

                // A block collection's end event sits wherever the next token starts,
                // past any comments, so its real end is its last piece of content.
                var end = Start;
                if (Kind == NodeKind.Sequence)
                {
                    foreach (var item in Items)
                        end = Math.Max(end, item.IsEmpty ? item.Start : item.ContentEnd);
                }
                else
                {
                    for (var i = 0; i < Keys.Count; i++)
                        end = Math.Max(end, EntryContentEnd(text, Keys[i], Values[i]));
                }

                ContentEnd = end;
                SpanEnd = EndOfLine(text, end);
            }
        }

        #endregion

        #region Merger

        private sealed class Merger
        {
            private readonly string _o;
            private readonly string _u;

            public Merger(string original, string updatedCanonical)
            {
                _o = original;
                _u = updatedCanonical;
            }

            /// <summary>
            /// New text for the region [o.Start, o.SpanEnd) of the original, or null when
            /// the node cannot be patched in place (the caller then replaces a bigger unit).
            /// </summary>
            /// <param name="oAnchor">Column of the key or dash that owns <paramref name="o"/> in the original.</param>
            /// <param name="rAnchor">Column of the key or dash that owns <paramref name="r1"/> in the updated text.</param>
            public string? Render(Node o, Node? r0, Node r1, int oAnchor, int rAnchor, bool root = false)
            {
                if (o.Kind == NodeKind.Alias || o.HasAnchor || o.IsEmpty)
                    return null;

                if (r0 != null && o.IsBlockCollection && r1.IsBlockCollection && o.Kind == r0.Kind && r0.Kind == r1.Kind)
                {
                    return o.Kind == NodeKind.Mapping
                        ? MergeMapping(o, r0, r1)
                        : MergeSequence(o, r0, r1, root: false);
                }

                var text = ReplacementText(o, r1, oAnchor, rAnchor, root);
                if (text == null)
                    return null;

                // Keep whatever followed the content on its last line (a trailing comment).
                return text + _o.Substring(o.ContentEnd, o.SpanEnd - o.ContentEnd);
            }

            private string? ReplacementText(Node o, Node r1, int oAnchor, int rAnchor, bool root)
            {
                if (r1.Kind == NodeKind.Alias)
                    return null;

                if (r1.Kind == NodeKind.Scalar)
                    return ScalarText(o, r1, oAnchor, rAnchor);

                // An empty collection is written as [] or {} and fits anywhere.
                if (r1.Flow)
                    return Slice(_u, r1.Start, r1.ContentEnd);

                if (o.Flow && !root)
                {
                    // Keep a flow list flow: [1, 2] stays on its line.
                    var flow = FlowText(r1);
                    if (flow != null)
                        return flow;
                }

                if (o.Kind == NodeKind.Scalar)
                    return null;

                if (!CanPlaceBlock(_o, o.Start))
                    return null;

                return Reindent(Slice(_u, r1.Start, r1.ContentEnd), Column(_o, o.Start) - Column(_u, r1.Start));
            }

            private string? ScalarText(Node o, Node r1, int oAnchor, int rAnchor)
            {
                var raw = Slice(_u, r1.Start, r1.ContentEnd);

                // Keep the author's quoting ("text" or 'text') when the new value allows it.
                if (o.Kind == NodeKind.Scalar
                    && (o.Style == ScalarStyle.SingleQuoted || o.Style == ScalarStyle.DoubleQuoted)
                    && r1.Tag.Length == 0
                    && (r1.Style == ScalarStyle.Plain || r1.Style == ScalarStyle.SingleQuoted || r1.Style == ScalarStyle.DoubleQuoted)
                    && !(r1.Style == ScalarStyle.Plain && IsNullLike(r1.Value))
                    && r1.Value.IndexOf('\n') < 0)
                {
                    return o.Style == ScalarStyle.SingleQuoted
                        ? SingleQuote(r1.Value) ?? DoubleQuote(r1.Value)
                        : DoubleQuote(r1.Value);
                }

                if (o.Flow)
                    return raw.IndexOf('\n') < 0 ? raw : null;

                return Reindent(raw, oAnchor - rAnchor);
            }

            #region Mapping

            private sealed class Edit
            {
                public int Start;
                public int End;
                public string Text = string.Empty;
                public bool IsInsertion => Start == End;
            }

            public string? MergeMapping(Node o, Node r0, Node r1)
            {
                var oIndex = IndexKeys(o);
                var r0Index = IndexKeys(r0);
                var r1Index = IndexKeys(r1);
                if (oIndex == null || r0Index == null || r1Index == null)
                    return null;

                var oCol = Column(_o, o.Start);
                var rCol = Column(_u, r1.Start);
                var edits = new List<Edit>();
                var removed = new HashSet<int>();

                // Fields that are gone (a value became null and is omitted).
                for (var i = 0; i < r0.Keys.Count; i++)
                {
                    var key = r0.Keys[i].Value;
                    if (r1Index.ContainsKey(key) || !oIndex.TryGetValue(key, out var oi))
                        continue;

                    var removal = RemovalEdit(o, oi);
                    if (removal == null)
                        return null;
                    edits.Add(removal);
                    removed.Add(oi);
                }

                var insertions = new SortedDictionary<int, List<string>>();

                for (var p = 0; p < r1.Keys.Count; p++)
                {
                    var key = r1.Keys[p].Value;
                    var v1 = r1.Values[p];
                    Node? v0 = r0Index.TryGetValue(key, out var r0i) ? r0.Values[r0i] : null;

                    if (v0 != null && DeepEqual(v0, v1))
                        continue;

                    if (oIndex.TryGetValue(key, out var oi))
                    {
                        var ov = o.Values[oi];
                        var rendered = Render(ov, v0, v1, oCol, rCol);
                        if (rendered != null)
                        {
                            edits.Add(new Edit { Start = ov.Start, End = ov.SpanEnd, Text = rendered });
                        }
                        else
                        {
                            // Rewrite the whole "Key: value" entry; its trailing comment stays.
                            edits.Add(new Edit
                            {
                                Start = o.Keys[oi].Start,
                                End = EntryContentEnd(_o, o.Keys[oi], ov),
                                Text = EntryText(r1, p, oCol),
                            });
                        }
                        continue;
                    }

                    // A field the file did not spell out: insert it after its predecessor.
                    var after = -1;
                    for (var q = p - 1; q >= 0 && after < 0; q--)
                    {
                        if (oIndex.TryGetValue(r1.Keys[q].Value, out var candidate) && !removed.Contains(candidate))
                            after = candidate;
                    }
                    if (after < 0)
                    {
                        for (var j = o.Keys.Count - 1; j >= 0 && after < 0; j--)
                        {
                            if (!removed.Contains(j))
                                after = j;
                        }
                    }
                    if (after < 0)
                        return null;

                    var position = EndOfLine(_o, EntryContentEnd(_o, o.Keys[after], o.Values[after]));
                    if (!insertions.TryGetValue(position, out var list))
                        insertions[position] = list = new List<string>();
                    list.Add("\n" + new string(' ', oCol) + EntryText(r1, p, oCol));
                }

                foreach (var insertion in insertions)
                    edits.Add(new Edit { Start = insertion.Key, End = insertion.Key, Text = string.Concat(insertion.Value) });

                return ApplyEdits(o.Start, o.SpanEnd, edits);
            }

            private Edit? RemovalEdit(Node o, int index)
            {
                // The first entry shares its line with "- " or the parent key; leave that
                // to the caller, which rewrites the whole mapping.
                if (index == 0)
                    return null;

                var key = o.Keys[index];
                var lineStart = LineStart(_o, key.Start);
                if (!IsBlank(Slice(_o, lineStart, key.Start)))
                    return null;

                var previousEnd = EntryContentEnd(_o, o.Keys[index - 1], o.Values[index - 1]);
                var keyColumn = key.Start - lineStart;
                var start = lineStart;

                // Comment lines directly above the field go with it (deeper-indented ones
                // close the previous field's nested block and stay).
                while (start > 0)
                {
                    var previousLineStart = LineStart(_o, start - 1);
                    if (previousLineStart < previousEnd)
                        break;
                    var line = Slice(_o, previousLineStart, start - 1);
                    if (!IsComment(line) || IndentOf(line) > keyColumn)
                        break;
                    start = previousLineStart;
                }

                if (start - 1 < o.Start)
                    return null;

                return new Edit
                {
                    Start = start - 1,
                    End = EndOfLine(_o, EntryContentEnd(_o, key, o.Values[index])),
                    Text = string.Empty,
                };
            }

            private string EntryText(Node r1, int index, int targetColumn)
            {
                var key = r1.Keys[index];
                var text = Slice(_u, key.Start, EntryContentEnd(_u, key, r1.Values[index]));
                return Reindent(text, targetColumn - Column(_u, key.Start));
            }

            private string? ApplyEdits(int start, int end, List<Edit> edits)
            {
                edits.Sort((a, b) =>
                {
                    var byStart = a.Start.CompareTo(b.Start);
                    if (byStart != 0) return byStart;
                    // At the same position an insertion goes before a replacement.
                    return b.IsInsertion.CompareTo(a.IsInsertion);
                });

                var sb = new StringBuilder();
                var cursor = start;
                foreach (var edit in edits)
                {
                    if (edit.Start < cursor || edit.End > end)
                        return null;
                    sb.Append(_o, cursor, edit.Start - cursor);
                    sb.Append(edit.Text);
                    cursor = edit.End;
                }
                sb.Append(_o, cursor, end - cursor);
                return sb.ToString();
            }

            #endregion

            #region Sequence

            private sealed class Unit
            {
                /// <summary>Lines before the unit that are not attached to it (blank lines, section comments).</summary>
                public List<string> Detached = new List<string>();
                /// <summary>Comment lines directly above the dash.</summary>
                public List<string> Attached = new List<string>();
                /// <summary>Indentation of the dash line.</summary>
                public string Indent = string.Empty;
                /// <summary>From the dash to the end of the item's last line.</summary>
                public string Body = string.Empty;
                /// <summary>Indented comment lines right after the item.</summary>
                public List<string> Trailing = new List<string>();
            }

            /// <summary>
            /// New text for a block sequence. For the document root this is the whole
            /// document (header and footer included); otherwise the region [o.Start, o.SpanEnd).
            /// </summary>
            public string? MergeSequence(Node o, Node r0, Node r1, bool root)
            {
                var n = o.Items.Count;
                if (n == 0 || r0.Items.Count != n || o.Flow)
                    return null;

                var dash = new int[n];
                var bodyEnd = new int[n];
                for (var j = 0; j < n; j++)
                {
                    var item = o.Items[j];
                    if (item.Kind == NodeKind.Alias || item.HasAnchor)
                        return null;
                    dash[j] = FindDash(_o, item);
                    if (dash[j] < 0)
                        return null;
                    bodyEnd[j] = EndOfLine(_o, ItemContentEnd(item, dash[j]));
                }

                var seqCol = Column(_o, dash[0]);
                var regionStart = root ? 0 : o.Start;
                var regionEnd = root ? _o.Length : o.SpanEnd;
                if (regionStart > dash[0] || bodyEnd[n - 1] > regionEnd)
                    return null;

                var units = new Unit[n];
                for (var j = 0; j < n; j++)
                    units[j] = new Unit { Body = Slice(_o, dash[j], bodyEnd[j]) };

                // Before the first dash: the file header (root) and the first item's comments.
                {
                    var lineStart = LineStart(_o, dash[0]);
                    var indent = Slice(_o, lineStart, dash[0]);
                    if (!IsBlank(indent))
                        return null;
                    units[0].Indent = indent;
                    if (root)
                    {
                        var before = Slice(_o, 0, lineStart);
                        var lines = SplitLines(before);
                        SplitAttached(lines, units[0].Detached, units[0].Attached);
                    }
                }

                // Between items.
                for (var j = 1; j < n; j++)
                {
                    var gap = Slice(_o, bodyEnd[j - 1], dash[j]);
                    if (gap.Length == 0 || gap[0] != '\n')
                        return null;
                    var pieces = gap.Substring(1).Split('\n');
                    var indent = pieces[pieces.Length - 1];
                    if (!IsBlank(indent))
                        return null;

                    var k = 0;
                    while (k < pieces.Length - 1 && IsComment(pieces[k]) && IndentOf(pieces[k]) > seqCol)
                        units[j - 1].Trailing.Add(pieces[k++]);

                    var rest = new List<string>();
                    for (; k < pieces.Length - 1; k++)
                        rest.Add(pieces[k]);
                    SplitAttached(rest, units[j].Detached, units[j].Attached);
                    units[j].Indent = indent;
                }

                // After the last item (root only; a nested sequence ends at its last line).
                var suffix = Slice(_o, bodyEnd[n - 1], regionEnd);
                if (root && suffix.Length > 0 && suffix[0] == '\n')
                {
                    var pieces = suffix.Substring(1).Split('\n');
                    var k = 0;
                    while (k < pieces.Length - 1 && IsComment(pieces[k]) && IndentOf(pieces[k]) > seqCol)
                        units[n - 1].Trailing.Add(pieces[k++]);
                    suffix = "\n" + string.Join("\n", pieces, k, pieces.Length - k);
                }

                var match = MatchItems(r0.Items, r1.Items);
                var survives = new bool[n];
                foreach (var j in match)
                {
                    if (j >= 0)
                        survives[j] = true;
                }

                // Section comments of deleted items move to the next surviving item.
                var detached = new List<string>[n];
                var carry = new List<string>();
                for (var j = 0; j < n; j++)
                {
                    var own = j == 0 ? new List<string>() : units[j].Detached;
                    if (!survives[j])
                    {
                        if (own.Any(IsComment))
                            carry = MergeBlankLines(carry, own);
                        continue;
                    }
                    detached[j] = MergeBlankLines(carry, own);
                    carry = new List<string>();
                }

                // What goes above an added item: the blank lines this file puts between items.
                var separator = new List<string>();
                if (n >= 2 && units[n - 1].Detached.All(IsBlank))
                    separator = units[n - 1].Detached;

                var sb = new StringBuilder();
                foreach (var line in units[0].Detached)
                    sb.Append(line).Append('\n');

                var first = true;
                var oDashCol = seqCol;
                for (var k = 0; k < r1.Items.Count; k++)
                {
                    var j = match[k];
                    List<string> lead, attached, trailing;
                    string indent, body;

                    if (j >= 0)
                    {
                        var unit = units[j];
                        lead = detached[j];
                        attached = unit.Attached;
                        trailing = unit.Trailing;
                        indent = unit.Indent;
                        if (DeepEqual(r0.Items[j], r1.Items[k]))
                        {
                            body = unit.Body;
                        }
                        else
                        {
                            var changed = ChangedItemBody(o.Items[j], dash[j], bodyEnd[j], r0.Items[j], r1.Items[k]);
                            if (changed == null)
                                return null;
                            body = changed;
                        }
                    }
                    else
                    {
                        lead = separator;
                        attached = new List<string>();
                        trailing = new List<string>();
                        indent = new string(' ', oDashCol);
                        var added = ItemText(r1.Items[k], oDashCol);
                        if (added == null)
                            return null;
                        body = added;
                    }

                    // A nested sequence starts mid-line, right where its first dash was.
                    var atDash = first && !root;

                    if (!first)
                        sb.Append('\n');
                    if (first)
                    {
                        var header = units[0].Detached;
                        if (!root)
                            lead = new List<string>(); // right under the parent key
                        else if (header.Count == 0 || IsBlank(header[header.Count - 1]))
                            lead = lead.SkipWhile(IsBlank).ToList(); // no doubled blank line under the header
                    }

                    foreach (var line in lead)
                        sb.Append(line).Append('\n');
                    foreach (var line in attached)
                    {
                        sb.Append(atDash ? line.TrimStart() : line).Append('\n');
                        atDash = false;
                    }
                    if (!atDash)
                        sb.Append(indent);
                    sb.Append(body);
                    foreach (var line in trailing)
                        sb.Append('\n').Append(line);

                    first = false;
                }

                if (first)
                {
                    // Every item is gone.
                    if (!root)
                        return null;
                    sb.Append(units[0].Indent).Append("[]");
                }

                foreach (var line in carry)
                    sb.Append('\n').Append(line);

                sb.Append(suffix);
                return sb.ToString();
            }

            private string? ChangedItemBody(Node item, int dashIndex, int bodyEnd, Node r0Item, Node r1Item)
            {
                var r1Dash = FindDash(_u, r1Item);
                if (r1Dash < 0)
                    return null;

                if (!item.IsEmpty)
                {
                    var rendered = Render(item, r0Item, r1Item, Column(_o, dashIndex), Column(_u, r1Dash));
                    if (rendered != null)
                        return Slice(_o, dashIndex, item.Start) + rendered + Slice(_o, item.SpanEnd, bodyEnd);
                }

                // Rewrite the whole item; a comment at the end of its last line stays.
                var text = ItemText(r1Item, Column(_o, dashIndex));
                if (text == null)
                    return null;
                return text + Slice(_o, ItemContentEnd(item, dashIndex), bodyEnd);
            }

            /// <summary>An item of the updated text, from its dash, placed at <paramref name="dashColumn"/>.</summary>
            private string? ItemText(Node r1Item, int dashColumn)
            {
                var r1Dash = FindDash(_u, r1Item);
                if (r1Dash < 0)
                    return null;
                var text = Slice(_u, r1Dash, ItemContentEnd(r1Item, r1Dash));
                return Reindent(text, dashColumn - Column(_u, r1Dash));
            }

            /// <summary>
            /// For each updated item, the index of the original item it continues, or -1 for a new one.
            /// Rows with an <c>Id</c> are matched by it (so reorders and deletes keep their comments);
            /// other lists are aligned on the items that did not change.
            /// </summary>
            private static int[] MatchItems(List<Node> r0, List<Node> r1)
            {
                var match = new int[r1.Count];

                var ids0 = TryGetIds(r0);
                var ids1 = ids0 == null ? null : TryGetIds(r1);
                if (ids0 != null && ids1 != null)
                {
                    var byId = new Dictionary<string, int>();
                    for (var j = 0; j < ids0.Count; j++)
                        byId[ids0[j]] = j;
                    for (var k = 0; k < r1.Count; k++)
                        match[k] = byId.TryGetValue(ids1[k], out var j) ? j : -1;
                    return match;
                }

                if ((long)r0.Count * r1.Count > 250_000)
                {
                    for (var k = 0; k < r1.Count; k++)
                        match[k] = k < r0.Count ? k : -1;
                    return match;
                }

                // Longest common subsequence of unchanged items...
                var n = r0.Count;
                var m = r1.Count;
                var lcs = new int[n + 1, m + 1];
                for (var j = n - 1; j >= 0; j--)
                {
                    for (var k = m - 1; k >= 0; k--)
                    {
                        lcs[j, k] = DeepEqual(r0[j], r1[k])
                            ? lcs[j + 1, k + 1] + 1
                            : Math.Max(lcs[j + 1, k], lcs[j, k + 1]);
                    }
                }

                for (var k = 0; k < m; k++)
                    match[k] = -1;

                var anchors = new List<(int j, int k)>();
                {
                    int j = 0, k = 0;
                    while (j < n && k < m)
                    {
                        if (DeepEqual(r0[j], r1[k]))
                        {
                            anchors.Add((j, k));
                            j++;
                            k++;
                        }
                        else if (lcs[j + 1, k] >= lcs[j, k + 1])
                        {
                            j++;
                        }
                        else
                        {
                            k++;
                        }
                    }
                }
                anchors.Add((n, m));

                // ...and between them, changed items pair up in order.
                int pj = 0, pk = 0;
                foreach (var (aj, ak) in anchors)
                {
                    var pairs = Math.Min(aj - pj, ak - pk);
                    for (var i = 0; i < pairs; i++)
                        match[pk + i] = pj + i;
                    if (ak < m)
                        match[ak] = aj;
                    pj = aj + 1;
                    pk = ak + 1;
                }

                return match;
            }

            private static List<string>? TryGetIds(List<Node> items)
            {
                var ids = new List<string>(items.Count);
                var seen = new HashSet<string>();
                foreach (var item in items)
                {
                    if (item.Kind != NodeKind.Mapping)
                        return null;
                    string? id = null;
                    for (var i = 0; i < item.Keys.Count; i++)
                    {
                        if (item.Keys[i].Kind == NodeKind.Scalar && item.Keys[i].Value == "Id" && item.Values[i].Kind == NodeKind.Scalar)
                        {
                            id = item.Values[i].Value;
                            break;
                        }
                    }
                    if (id == null || !seen.Add(id))
                        return null;
                    ids.Add(id);
                }
                return ids;
            }

            private static void SplitAttached(List<string> lines, List<string> detached, List<string> attached)
            {
                var split = lines.Count;
                while (split > 0 && IsComment(lines[split - 1]))
                    split--;
                for (var i = 0; i < split; i++)
                    detached.Add(lines[i]);
                for (var i = split; i < lines.Count; i++)
                    attached.Add(lines[i]);
            }

            private static List<string> MergeBlankLines(List<string> first, List<string> second)
            {
                var result = new List<string>(first);
                var skipBlank = result.Count > 0 && IsBlank(result[result.Count - 1]);
                foreach (var line in second)
                {
                    if (skipBlank && IsBlank(line))
                        continue;
                    skipBlank = false;
                    result.Add(line);
                }
                return result;
            }

            #endregion

            #region Flow rendering

            private string? FlowText(Node node)
            {
                switch (node.Kind)
                {
                    case NodeKind.Scalar:
                    {
                        if (node.Tag.Length > 0)
                            return null;
                        if (node.Style == ScalarStyle.Literal || node.Style == ScalarStyle.Folded || node.Value.IndexOf('\n') >= 0)
                            return DoubleQuote(node.Value);
                        var raw = Slice(_u, node.Start, node.ContentEnd);
                        if (node.Style == ScalarStyle.Plain)
                        {
                            if (node.Value.Length == 0)
                                return "null";
                            if (raw.IndexOfAny(FlowIndicators) >= 0 || raw.Contains(": ") || raw.Contains(" #"))
                                return DoubleQuote(node.Value);
                        }
                        return raw.IndexOf('\n') < 0 ? raw : DoubleQuote(node.Value);
                    }

                    case NodeKind.Sequence:
                    {
                        var parts = new List<string>();
                        foreach (var item in node.Items)
                        {
                            var part = FlowText(item);
                            if (part == null)
                                return null;
                            parts.Add(part);
                        }
                        return "[" + string.Join(", ", parts) + "]";
                    }

                    case NodeKind.Mapping:
                    {
                        var parts = new List<string>();
                        for (var i = 0; i < node.Keys.Count; i++)
                        {
                            var key = FlowText(node.Keys[i]);
                            var value = FlowText(node.Values[i]);
                            if (key == null || value == null)
                                return null;
                            parts.Add(key + ": " + value);
                        }
                        return "{" + string.Join(", ", parts) + "}";
                    }

                    default:
                        return null;
                }
            }

            private static readonly char[] FlowIndicators = { ',', '[', ']', '{', '}' };

            #endregion
        }

        #endregion

        #region Helpers

        private static bool DeepEqual(Node a, Node b)
        {
            if (a.Kind != b.Kind || a.Tag != b.Tag)
                return false;

            switch (a.Kind)
            {
                case NodeKind.Scalar:
                    return a.Value == b.Value && a.Style == b.Style;

                case NodeKind.Sequence:
                    if (a.Items.Count != b.Items.Count)
                        return false;
                    for (var i = 0; i < a.Items.Count; i++)
                    {
                        if (!DeepEqual(a.Items[i], b.Items[i]))
                            return false;
                    }
                    return true;

                case NodeKind.Mapping:
                    if (a.Keys.Count != b.Keys.Count)
                        return false;
                    for (var i = 0; i < a.Keys.Count; i++)
                    {
                        if (!DeepEqual(a.Keys[i], b.Keys[i]) || !DeepEqual(a.Values[i], b.Values[i]))
                            return false;
                    }
                    return true;

                default:
                    return false;
            }
        }

        private static Dictionary<string, int>? IndexKeys(Node mapping)
        {
            var index = new Dictionary<string, int>();
            for (var i = 0; i < mapping.Keys.Count; i++)
            {
                var key = mapping.Keys[i];
                if (key.Kind != NodeKind.Scalar || key.HasAnchor || index.ContainsKey(key.Value))
                    return null;
                index[key.Value] = i;
            }
            return index;
        }

        /// <summary>End of "Key: value" content; for "Key:" with no value, just past the colon.</summary>
        private static int EntryContentEnd(string text, Node key, Node value)
        {
            if (!value.IsEmpty)
                return Math.Max(key.ContentEnd, value.ContentEnd);

            var i = key.ContentEnd;
            while (i < text.Length && text[i] == ' ')
                i++;
            return i < text.Length && text[i] == ':' ? i + 1 : key.ContentEnd;
        }

        private static int ItemContentEnd(Node item, int dashIndex)
            => item.IsEmpty ? dashIndex + 1 : item.ContentEnd;

        /// <summary>The '-' of a block sequence item, or -1.</summary>
        private static int FindDash(string text, Node item)
        {
            var i = item.Start - 1;
            while (i >= 0 && (text[i] == ' ' || text[i] == '\n'))
                i--;
            return i >= 0 && text[i] == '-' ? i : -1;
        }

        /// <summary>True when only indentation and "- " indicators precede <paramref name="index"/> on its line.</summary>
        private static bool CanPlaceBlock(string text, int index)
        {
            for (var i = LineStart(text, index); i < index; i++)
            {
                if (text[i] != ' ' && text[i] != '-')
                    return false;
            }
            return true;
        }

        private static int LineStart(string text, int index)
        {
            if (index <= 0)
                return 0;
            var newline = text.LastIndexOf('\n', index - 1);
            return newline + 1;
        }

        private static int EndOfLine(string text, int index)
        {
            var newline = text.IndexOf('\n', Math.Min(index, text.Length));
            return newline < 0 ? text.Length : newline;
        }

        private static int Column(string text, int index) => index - LineStart(text, index);

        private static string Slice(string text, int start, int end) => text.Substring(start, end - start);

        private static List<string> SplitLines(string block)
        {
            // "a\nb\n" -> [a, b]; "" -> []
            var lines = new List<string>(block.Split('\n'));
            if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            return lines;
        }

        private static bool IsBlank(string line) => string.IsNullOrWhiteSpace(line);

        private static bool IsComment(string line) => line.TrimStart().StartsWith("#", StringComparison.Ordinal);

        private static int IndentOf(string line)
        {
            var i = 0;
            while (i < line.Length && line[i] == ' ')
                i++;
            return i;
        }

        private static bool IsNullLike(string value)
            => value.Length == 0 || value == "~" || value == "null" || value == "Null" || value == "NULL";

        /// <summary>Shifts every line after the first by <paramref name="shift"/> columns.</summary>
        private static string Reindent(string text, int shift)
        {
            if (shift == 0 || text.IndexOf('\n') < 0)
                return text;

            var lines = text.Split('\n');
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                    continue;
                if (shift > 0)
                {
                    lines[i] = new string(' ', shift) + line;
                }
                else
                {
                    var remove = Math.Min(-shift, IndentOf(line));
                    lines[i] = line.Substring(remove);
                }
            }
            return string.Join("\n", lines);
        }

        private static string? SingleQuote(string value)
        {
            foreach (var c in value)
            {
                if (c < ' ')
                    return null;
            }
            return "'" + value.Replace("'", "''") + "'";
        }

        private static string DoubleQuote(string value)
        {
            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("X4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        #endregion
    }
}
