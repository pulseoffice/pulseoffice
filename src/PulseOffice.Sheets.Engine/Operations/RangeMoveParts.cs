using PulseOffice.Sheets.Addressing;

namespace PulseOffice.Sheets.Engine.Operations;

internal sealed class MovedRulesPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var carried = new List<Formatting.ConditionalRule>();
        new RangeMoveSplitter(edit).SplitRules(edit.Source);

        foreach (var sheet in edit.Workbook.Sheets)
        {
            var rules = sheet.ConditionalRules;
            var rewriter = edit.Rewriter(sheet.Name);
            for (var index = rules.Count - 1; index >= 0; index--)
            {
                var rule = rules[index];
                if (rule.Ranges.All(range => move.Moves(sheet.Name, range)))
                {
                    var moved = rule with
                    {
                        Range = move.Moved(rule.Range),
                        AdditionalRanges = [.. rule.AdditionalRanges.Select(move.Moved)],
                        Formula = edit.Rewrite(new MoveRewriter(move, sheet.Name, move.TargetSheet), rule.Formula),
                    };
                    if (edit.CrossSheet) { rules.RemoveAt(index); carried.Insert(0, moved); }
                    else rules[index] = moved;
                }
                else if (rule.Ranges.All(range => move.Overwrites(sheet.Name, range))) rules.RemoveAt(index);
                else rules[index] = rule with { Formula = edit.Rewrite(rewriter, rule.Formula) };
            }
        }

        edit.Target.ConditionalRules.AddRange(carried);
    }
}

internal sealed class MovedValidationsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var carried = new List<DataValidation>();
        new RangeMoveSplitter(edit).SplitValidations(edit.Source);

        foreach (var sheet in edit.Workbook.Sheets)
        {
            var validations = sheet.Validations;
            var rewriter = edit.Rewriter(sheet.Name);
            for (var index = validations.Count - 1; index >= 0; index--)
            {
                var validation = validations[index];
                if (move.Moves(sheet.Name, validation.Range))
                {
                    var carrier = new MoveRewriter(move, sheet.Name, move.TargetSheet);
                    var moved = validation with
                    {
                        Range = move.Moved(validation.Range),
                        FirstFormula = edit.Rewrite(carrier, validation.FirstFormula),
                        SecondFormula = edit.Rewrite(carrier, validation.SecondFormula),
                    };
                    if (edit.CrossSheet) { validations.RemoveAt(index); carried.Insert(0, moved); }
                    else validations[index] = moved;
                }
                else if (move.Overwrites(sheet.Name, validation.Range)) validations.RemoveAt(index);
                else validations[index] = validation with
                {
                    FirstFormula = edit.Rewrite(rewriter, validation.FirstFormula),
                    SecondFormula = edit.Rewrite(rewriter, validation.SecondFormula),
                };
            }
        }

        edit.Target.Validations.AddRange(carried);
    }
}

internal sealed class MovedCommentsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var moved = edit.Source.Comments.Where(pair => move.Source.Contains(pair.Key)).ToList();

        foreach (var (address, _) in moved) edit.Source.Comments.Remove(address);
        foreach (var address in edit.Target.Comments.Keys.Where(move.TargetArea.Contains).ToList()) edit.Target.Comments.Remove(address);
        foreach (var (address, comment) in moved) edit.Target.Comments[move.Moved(address)] = comment;
    }
}

internal sealed class MovedMergesPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var moved = edit.Source.Merges.All.Where(region => move.Inside(region, move.Source)).ToList();

        Keep(edit.Source, region => !move.Inside(region, move.Source));
        Keep(edit.Target, region => !move.Inside(region, move.TargetArea));
        edit.Target.Merges.AddRange(moved.Select(move.Moved));
    }

    private void Keep(Worksheet sheet, Func<CellRange, bool> kept)
    {
        var regions = sheet.Merges.All.Where(kept).ToList();
        sheet.Merges.Clear();
        sheet.Merges.AddRange(regions);
    }
}

internal sealed class MovedObjectsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var objects = edit.Source.Objects;
        var carried = new List<PlacedObject>();
        var requalifier = new FormControlRequalifier();

        for (var index = objects.Count - 1; index >= 0; index--)
        {
            var placed = objects[index];
            if (!move.Source.Contains(placed.Anchor)) continue;

            var moved = placed with { Anchor = move.Moved(placed.Anchor) };
            if (edit.CrossSheet)
            {
                objects.RemoveAt(index);
                carried.Insert(0, moved with { Content = requalifier.Carried(placed.Content, edit.Source.Name, edit.Target.Name) });
            }
            else objects[index] = moved;
        }

        edit.Target.Objects.AddRange(carried);
    }
}

internal sealed class MovedSparklinesPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var carried = new List<SparklineGroup>();

        foreach (var sheet in edit.Workbook.Sheets)
        {
            var groups = new List<SparklineGroup>();
            foreach (var group in sheet.Sparklines.Groups)
            {
                var staying = new List<Sparkline>();
                var leaving = new List<Sparkline>();
                foreach (var sparkline in group.Sparklines)
                {
                    var cellMoves = move.Moves(sheet.Name, new CellRange(sparkline.Cell, sparkline.Cell));
                    var sourceMoves = move.Moves(sheet.Name, sparkline.Source);
                    if (move.Overwrites(sheet.Name, new CellRange(sparkline.Cell, sparkline.Cell))) continue;
                    if (cellMoves && edit.CrossSheet && sourceMoves)
                        leaving.Add(new Sparkline(move.Moved(sparkline.Cell), move.Moved(sparkline.Source)));
                    else if (edit.CrossSheet && (cellMoves || sourceMoves))
                        staying.Add(sparkline);
                    else
                        staying.Add(new Sparkline(cellMoves ? move.Moved(sparkline.Cell) : sparkline.Cell, sourceMoves ? move.Moved(sparkline.Source) : sparkline.Source));
                }
                if (staying.Count > 0) groups.Add(group with { Sparklines = staying });
                if (leaving.Count > 0) carried.Add(group with { Id = Guid.NewGuid().ToString("N"), Sparklines = leaving });
            }
            sheet.Sparklines.Clear();
            sheet.Sparklines.AddRange(groups);
        }

        edit.Target.Sparklines.AddRange(carried);
    }
}

internal sealed class MovedPivotsPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        var pivots = edit.Workbook.Pivots;

        for (var index = 0; index < pivots.Count; index++)
        {
            var pivot = pivots[index];
            if (pivot.IsExternal || !move.Moves(pivot.SourceSheet, pivot.SourceRange)) continue;
            pivots[index] = pivot with { SourceSheet = move.TargetSheet, SourceRange = move.Moved(pivot.SourceRange) };
        }
    }
}

internal sealed class MovedChangesPart : RangeMovePart
{
    public override void Apply(RangeMoveEdit edit)
    {
        var move = edit.Move;
        edit.Workbook.Changes.Relocate(edit.SourceIndex, edit.TargetIndex,
            address => move.Source.Contains(address) ? move.Moved(address) : null);
    }
}
