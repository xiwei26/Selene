import SwiftUI
import AppKit

@Observable
@MainActor
final class ThemeStore {
    enum ThemeMode: String, Codable, CaseIterable, Identifiable {
        case system, light, dark

        var id: String { rawValue }

        var title: String {
            switch self {
            case .system: return "跟随系统"
            case .light: return "浅色"
            case .dark: return "深色"
            }
        }
    }

    var mode: ThemeMode {
        didSet {
            persistMode()
        }
    }

    private let userDefaults: UserDefaults
    private let key = "selene_theme_mode"

    init(userDefaults: UserDefaults = .standard) {
        self.userDefaults = userDefaults
        if let rawValue = userDefaults.string(forKey: key),
           let mode = ThemeMode(rawValue: rawValue) {
            self.mode = mode
        } else {
            self.mode = .system
        }
    }

    var colorScheme: ColorScheme? {
        switch mode {
        case .system: return nil
        case .light: return .light
        case .dark: return .dark
        }
    }

    var currentAppearance: NSAppearance {
        switch mode {
        case .system:
            return NSApp.effectiveAppearance
        case .light:
            return NSAppearance(named: .aqua) ?? NSApp.effectiveAppearance
        case .dark:
            return NSAppearance(named: .darkAqua) ?? NSApp.effectiveAppearance
        }
    }

    func toggleMode() {
        switch mode {
        case .system: mode = .light
        case .light: mode = .dark
        case .dark: mode = .system
        }
    }

    private func persistMode() {
        userDefaults.set(mode.rawValue, forKey: key)
    }
}
