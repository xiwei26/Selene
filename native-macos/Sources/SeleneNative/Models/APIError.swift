import Foundation

enum APIError: LocalizedError {
    case message(String)
    case responseError(statusCode: Int)
    case invalidURL
    case unauthorized
    case unknown

    var localizedDescription: String {
        switch self {
        case .message(let msg):
            return msg
        case .responseError(let code):
            return "请求失败 (\(code))"
        case .invalidURL:
            return "服务器地址无效"
        case .unauthorized:
            return "登录已过期，请重新登录"
        case .unknown:
            return "未知错误"
        }
    }
}
