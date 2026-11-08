using FolioPages;

return new PageCountRun(new PageCountComposition().Build(args.Skip(2).ToArray())).Run(args[0], args[1]);
