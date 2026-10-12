package bikecraft.core;

import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Tolerant reader for PiBoSo-style text configs: "key = value" lines, optionally inside
 * named "section { ... }" blocks. Keys come back as "section.key" (lower case).
 * Format is UNVERIFIED against real MX Bikes files; it accepts any mix of the two styles.
 */
public final class PiBoSoCfg {
    private PiBoSoCfg() {}

    public static Map<String, String> parse(String text) {
        Map<String, String> out = new LinkedHashMap<>();
        java.util.ArrayDeque<String> sections = new java.util.ArrayDeque<>();
        String pendingName = null;
        for (String raw : text.split("\\r?\\n")) {
            String line = raw;
            int c = indexOfComment(line);
            if (c >= 0) line = line.substring(0, c);
            line = line.trim();
            if (line.isEmpty()) continue;
            if (line.equals("{")) { sections.push(pendingName == null ? "" : pendingName); pendingName = null; continue; }
            if (line.equals("}")) { if (!sections.isEmpty()) sections.pop(); continue; }
            if (line.endsWith("{")) { sections.push(line.substring(0, line.length() - 1).trim().toLowerCase()); continue; }
            int eq = line.indexOf('=');
            if (eq < 0) { pendingName = line.toLowerCase(); continue; }
            String key = line.substring(0, eq).trim().toLowerCase();
            String val = line.substring(eq + 1).trim();
            if (val.length() >= 2 && val.startsWith("\"") && val.endsWith("\"")) val = val.substring(1, val.length() - 1);
            out.put(prefix(sections) + key, val);
        }
        return out;
    }

    private static int indexOfComment(String s) {
        int a = s.indexOf("//"), b = s.indexOf('#'), d = s.indexOf(';');
        int m = -1;
        for (int i : new int[]{a, b, d}) if (i >= 0 && (m < 0 || i < m)) m = i;
        return m;
    }

    private static String prefix(java.util.ArrayDeque<String> sections) {
        StringBuilder sb = new StringBuilder();
        var it = sections.descendingIterator();
        while (it.hasNext()) { String s = it.next(); if (!s.isEmpty()) sb.append(s).append('.'); }
        return sb.toString();
    }

    /** First numeric value whose key ends with any of the given suffixes. */
    public static Double find(Map<String, String> cfg, String... suffixes) {
        for (String suf : suffixes)
            for (var e : cfg.entrySet())
                if (e.getKey().equals(suf) || e.getKey().endsWith("." + suf)) {
                    Double d = firstNumber(e.getValue());
                    if (d != null) return d;
                }
        return null;
    }

    static Double firstNumber(String v) {
        var m = java.util.regex.Pattern.compile("-?\\d+(\\.\\d+)?").matcher(v);
        return m.find() ? Double.parseDouble(m.group()) : null;
    }
}
