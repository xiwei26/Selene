import Foundation

enum APIError: LocalizedError, Equatable {
    case invalidServerURL
    case invalidResponse
    case unauthorized
    case responseError(statusCode: Int)
    case decodingFailed
    case featureDisabled(String, statusCode: Int)
    case message(String)

    var errorDescription: String? {
        switch self {
        case .invalidServerURL:
            return "服务器地址无效"
        case .invalidResponse:
            return "服务器返回了无效响应"
        case .unauthorized:
            return "用户名或密码错误，或登录已失效"
        case .responseError(let statusCode):
            return "服务器请求失败（HTTP \(statusCode)）"
        case .decodingFailed:
            return "服务器数据格式暂时无法识别"
        case .featureDisabled(let message, _):
            return message
        case .message(let message):
            return message
        }
    }

    var isFeatureDisabled: Bool {
        if case .featureDisabled = self { return true }
        return false
    }
}