import XCTest
@testable import SeleneNative

@MainActor
final class FeatureParityP3Tests: XCTestCase {
    func testSubscriptionServiceParsesBase58EncodedResourcesAndLives() {
        let json = """
        {
          "api_site": {
            "siteA": {
              "api": "https://example.com/api",
              "name": "站点A",
              "detail": "detail",
              "from": "remote",
              "disabled": false
            }
          },
          "lives": [
            {
              "key": "liveA",
              "name": "直播A",
              "url": "https://example.com/live.m3u",
              "ua": "UA",
              "epg": "https://example.com/epg.xml",
              "from": "remote",
              "disabled": false
            }
          ]
        }
        """

        let encoded = Base58.encode([UInt8](json.utf8))
        let content = SubscriptionService.parseSubscriptionContent(encoded)

        XCTAssertEqual(content?.searchResources?.first?.key, "siteA")
        XCTAssertEqual(content?.searchResources?.first?.name, "站点A")
        XCTAssertEqual(content?.liveSources?.first?.key, "liveA")
    }

    func testLiveServiceParsesM3UChannels() throws {
        let m3u = """
        #EXTM3U
        #EXTINF:-1 tvg-id="cctv1" tvg-logo="logo.png" group-title="央视",CCTV-1
        https://example.com/cctv1.m3u8
        #EXTINF:-1 group-title="卫视",湖南卫视
        https://example.com/hunan.m3u8
        """

        let channels = try LiveServiceClient.parseM3U(m3u)

        XCTAssertEqual(channels.count, 2)
        XCTAssertEqual(channels[0].tvgId, "cctv1")
        XCTAssertEqual(channels[0].name, "CCTV-1")
        XCTAssertEqual(channels[0].group, "央视")
        XCTAssertEqual(channels[1].id, "湖南卫视-https://example.com/hunan.m3u8")
    }

    func testLiveServiceParsesEPGProgrammes() throws {
        let xml = """
        <tv>
          <programme channel="cctv1" start="20240614080000 +0800" stop="20240614090000 +0800">
            <title>新闻</title>
            <desc>早间新闻</desc>
          </programme>
        </tv>
        """

        let epg = try LiveServiceClient.parseEPG(xml, tvgId: "cctv1", source: "liveA", epgUrl: "epg.xml")

        XCTAssertEqual(epg.programs.count, 1)
        XCTAssertEqual(epg.programs[0].title, "新闻")
        XCTAssertEqual(epg.programs[0].description, "早间新闻")
    }

    func testLiveStoreGroupsAndFiltersChannels() {
        let store = LiveStore()
        store.channels = [
            LiveChannel(id: "1", tvgId: "1", name: "A", logo: "", group: "新闻", url: "u1", isFavorite: false),
            LiveChannel(id: "2", tvgId: "2", name: "B", logo: "", group: "体育", url: "u2", isFavorite: false),
            LiveChannel(id: "3", tvgId: "3", name: "C", logo: "", group: "新闻", url: "u3", isFavorite: false)
        ]

        store.rebuildGroups()
        store.filterByGroup("新闻")

        XCTAssertEqual(store.channelGroups.count, 2)
        XCTAssertEqual(store.filteredChannels.map(\.name), ["A", "C"])
    }
}
