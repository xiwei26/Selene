import Foundation

enum APIError: LocalizedError {
    case message(String)
    case responseError(statusCode: Int)
    case featureDisabled(String, statusCode: Int?)
    case invalidURL
    case unauthorized
    case networkTimeout
    case sseConnectionFailed
    case parseError
    case noResults
    case unknown

    var errorDescription: String? {
        messageText
    }

    var localizedDescription: String {
        messageText
    }

    private var messageText: String {
        switch self {
        case .message(let msg):
            return msg
        case .responseError(let code):
            return "请求失败 (\(code))"
        case .featureDisabled(let msg, _):
            return msg
        case .invalidURL:
            return "服务器地址无效"
        case .unauthorized:
            return "登录已过期，请重新登录"
        case .networkTimeout:
            return "请求超时"
        case .sseConnectionFailed:
            return "实时搜索连接已断开"
        case .parseError:
            return "数据解析失败"
        case .noResults:
            return "没有找到结果"
        case .unknown:
            return "未知错误"
        }
    }

    var isFeatureDisabled: Bool {
        if case .featureDisabled = self {
            return true
        }
        return false
    }
}
