(defproject jepsen.elle "0.1.0-SNAPSHOT"
  :description "FIXME: write description"
  :url "http://example.com/FIXME"
  :license {:name "EPL-2.0 OR GPL-2.0-or-later WITH Classpath-exception-2.0"
            :url "https://www.eclipse.org/legal/epl-2.0/"}
  :main jepsen.elle
  :dependencies [[org.clojure/clojure "1.10.3"]
                 [jepsen "0.2.7"]
                 [verschlimmbesserung "0.1.3"]
                 [clj-http "3.12.3"]
                 [org.clojure/data.json "2.4.0"]]
  :repl-options {:init-ns jepsen.elle})
