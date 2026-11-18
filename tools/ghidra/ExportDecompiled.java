// Every function of the analysed program as C, into files of 64 KB of code each (by image-relative address), for
// searching with grep. Run by tools/decompile_game.py after Ghidra's auto-analysis:
//   ExportDecompiled.java <output folder>
// Each function starts with a line "// ==== <rva> <name> (<size> bytes)". Functions are decompiled in parallel
// (ParallelDecompiler), a batch at a time so the text never all sits in memory.
//@category Export

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileOptions;
import ghidra.app.decompiler.DecompileResults;
import ghidra.app.decompiler.parallel.DecompileConfigurer;
import ghidra.app.decompiler.parallel.DecompilerCallback;
import ghidra.app.decompiler.parallel.ParallelDecompiler;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.listing.Function;
import ghidra.util.task.TaskMonitor;

public class ExportDecompiled extends GhidraScript {
    private static final int BATCH = 2000;

    @Override
    protected void run() throws Exception {
        String[] args = getScriptArgs();
        File out = new File(args.length > 0 ? args[0] : "decompiled");
        out.mkdirs();
        long base = currentProgram.getImageBase().getOffset();

        List<Function> all = new ArrayList<>();
        for (Function f : currentProgram.getFunctionManager().getFunctions(true))
            if (!f.isExternal() && !f.isThunk())
                all.add(f);
        println("functions: " + all.size());

        DecompileConfigurer configurer = new DecompileConfigurer() {
            @Override
            public void configure(DecompInterface decompiler) {
                decompiler.toggleCCode(true);
                decompiler.toggleSyntaxTree(false);
                decompiler.setSimplificationStyle("decompile");
                DecompileOptions options = new DecompileOptions();
                options.grabFromProgram(currentProgram);
                decompiler.setOptions(options);
            }
        };
        DecompilerCallback<String[]> callback = new DecompilerCallback<>(currentProgram, configurer) {
            @Override
            public String[] process(DecompileResults results, TaskMonitor monitor) {
                Function f = results.getFunction();
                long rva = f.getEntryPoint().getOffset() - base;
                String head = String.format("// ==== 0x%X %s (%d bytes)%n", rva, f.getName(), f.getBody().getNumAddresses());
                String body = results.getDecompiledFunction() != null
                    ? results.getDecompiledFunction().getC()
                    : "// (not decompiled: " + results.getErrorMessage() + ")\n";
                return new String[] { Long.toString(rva), head + body + "\n" };
            }
        };
        callback.setTimeout(60);

        int done = 0;
        try {
            for (int from = 0; from < all.size(); from += BATCH) {
                List<Function> batch = all.subList(from, Math.min(all.size(), from + BATCH));
                List<String[]> results = ParallelDecompiler.decompileFunctions(callback, batch, monitor);
                Map<Long, List<String[]>> files = new TreeMap<>();
                for (String[] r : results) {
                    if (r == null)
                        continue;
                    long rva = Long.parseLong(r[0]);
                    files.computeIfAbsent(rva >> 16, k -> new ArrayList<>()).add(r);
                }
                for (Map.Entry<Long, List<String[]>> e : files.entrySet()) {
                    e.getValue().sort((a, b) -> Long.compare(Long.parseLong(a[0]), Long.parseLong(b[0])));
                    File file = new File(out, String.format("%04X0000.c", e.getKey()));
                    try (FileWriter w = new FileWriter(file, true)) {
                        for (String[] r : e.getValue())
                            w.write(r[1]);
                    }
                }
                done += batch.size();
                println("decompiled " + done + " of " + all.size());
            }
        }
        finally {
            callback.dispose();
        }
    }
}
