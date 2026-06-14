import Foundation

struct DLNADevice: Identifiable, Hashable {
    var id: String { location }
    var name: String
    var location: String
}

@Observable
final class DLNADiscoveryService {
    var devices: [DLNADevice] = []
    var isSearching = false

    func startDiscovery() {
        isSearching = true
        devices = []
    }

    func stopDiscovery() {
        isSearching = false
    }
}
