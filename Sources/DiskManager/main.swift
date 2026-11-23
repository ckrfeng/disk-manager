import DiskCore
import Foundation

let arguments = CommandLine.arguments
if arguments.count == 4, arguments[1] == "--helper-scan" {
    guard let root = DiskScanner(path: arguments[2]).run(),
          let handle = FileHandle(forWritingAtPath: arguments[3]) else { exit(1) }
    do {
        try handle.truncate(atOffset: 0)
        try handle.write(contentsOf: TreeCoding.encode(root))
        try handle.close()
        exit(0)
    } catch {
        exit(1)
    }
}
DiskManagerApp.main()
