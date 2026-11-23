import Foundation

public enum Shell {
    public struct Failure: LocalizedError {
        public let status: Int32
        public let output: String
        public var errorDescription: String? {
            output.isEmpty ? "The command failed with status \(status)." : output
        }
        public var wasCancelled: Bool { output.contains("-128") }
    }

    public static func quote(_ argument: String) -> String {
        "'" + argument.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    @discardableResult
    public static func run(_ executable: String, _ arguments: [String]) throws -> Data {
        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        let out = Pipe(), err = Pipe()
        process.standardOutput = out
        process.standardError = err
        try process.run()
        var errData = Data()
        let group = DispatchGroup()
        DispatchQueue.global().async(group: group) { errData = err.fileHandleForReading.readDataToEndOfFile() }
        let data = out.fileHandleForReading.readDataToEndOfFile()
        group.wait()
        process.waitUntilExit()
        guard process.terminationStatus == 0 else {
            let message = String(decoding: errData.isEmpty ? data : errData, as: UTF8.self)
            throw Failure(status: process.terminationStatus,
                          output: message.trimmingCharacters(in: .whitespacesAndNewlines))
        }
        return data
    }

    @discardableResult
    public static func runAsAdministrator(_ executable: String, _ arguments: [String], prompt: String) throws -> Data {
        let command = ([executable] + arguments).map(quote).joined(separator: " ")
        let script = "do shell script \(appleScriptString(command)) with prompt \(appleScriptString(prompt)) "
            + "with administrator privileges"
        return try run("/usr/bin/osascript", ["-e", script])
    }

    private static func appleScriptString(_ string: String) -> String {
        "\"" + string.replacingOccurrences(of: "\\", with: "\\\\").replacingOccurrences(of: "\"", with: "\\\"") + "\""
    }
}
