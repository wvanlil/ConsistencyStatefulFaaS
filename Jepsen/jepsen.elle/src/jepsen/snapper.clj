(ns jepsen.snapper
  (:require [clojure.tools.logging :refer :all]
            [clojure.string :as str]
            [jepsen [cli :as cli]
                    [client :as client]
                    [control :as c]
                    [db :as db]
                    [generator :as gen]
                    [nemesis :as nemesis]
                    [tests :as tests]]
            [jepsen.control.util :as cu]
            [jepsen.os.debian :as debian]
            [elle.list-append :as ela]
            [clj-http.client :as httpclient]
            [slingshot.slingshot :refer [try+]]))

(def url "https://www.dropbox.com/scl/fi/dropboxlink/SnapperSiloHost_v1.3.tar.gz?rlkey=u8n8y4qf7k2t903iyazyfyb78&dl=0")

(def dir "/snapper")
(def binary "etcd")
(def logfile (str dir "/snapper.log"))
(def pidfile (str dir "/snapper.pid"))

(defn r   [_ _] {:type :invoke, :f :read, :value nil})
(defn w   [_ _] {:type :invoke, :f :write, :value (rand-int 5)})
(defn cas [_ _] {:type :invoke, :f :cas, :value [(rand-int 5) (rand-int 5)]})

(defn db
  "Etcd DB for a particular version."
  [version]
  (reify db/DB
    (setup! [_ test node]
      (info node "downloading and unpacking" version)

      (c/exec :apt-get :install :wget :-y)
      (c/exec :wget "https://packages.microsoft.com/config/debian/11/packages-microsoft-prod.deb" :-O :packages-microsoft-prod.deb)
      (c/exec :dpkg :-i :packages-microsoft-prod.deb)
      (c/exec :rm :packages-microsoft-prod.deb)
      (c/exec :apt-get :update)
      (c/exec :apt-get :install :-y :apt-transport-https)
      (c/exec :apt-get :update)
      (c/exec :apt-get :install :-y :dotnet-runtime-3.1)

      (cu/install-archive! url dir)
      (cu/start-daemon!
      {
        :logfile logfile
        :pidfile pidfile
        :chdir dir
      }
      "/usr/bin/dotnet" "/snapper/SnapperSiloHost.dll")

      (Thread/sleep 35000)
      )

    (teardown! [_ test node]
      (info node "tearing down silo")
      (cu/stop-daemon! "dotnet" pidfile)
      ;; (c/exec :rm :-rf dir)
      )

    ;; Logging asks for password, doesnt seem to work correctly...
    ;; db/LogFiles
    ;; (log-files [_ test node]
    ;;   [logfile])

    ))

(defrecord Client [conn]
  client/Client
  (open! [this test node]
    this)

  (setup! [this test])

  (invoke! [_ test op]
    (case (:f op)
      :txn (assoc op :type :ok, :value (try 
                                            (read-string (:body (httpclient/post (str "http://localhost:5000/" (if (< (rand) 2) "act-nogroup" "pact")) {:insecure? true :form-params {:operations (str (:value op))}
                                                                                            :content-type :json
                                                                                            })))
                                            (catch Exception e (throw (Exception. (.getMessage e)))) ;; Normal exception message receives error, but custom one loses information :(
                                                                                            )
      )
      
      
      )
  )

  (teardown! [this test])

  (close! [_ test]))

(defn etcd-test
  "Given an options map from the command line runner (e.g. :nodes, :ssh,
  :concurrency ...), constructs a test map."
  [opts]
  (merge tests/noop-test
         opts
         {:pure-generators true
          :name "etcd"
          :os   debian/os
          :db   (db "Snapper 0.1")
          :client (Client. nil)
          ;; :checker (ela/check) ;; Could not get to work
          ;; :nemesis (nemesis/partition-random-node)
          ;; :nemesis (nemesis/partition-random-halves)
          :nemesis (nemesis/compose {
            {
            :half-start :start
            :half-stop :stop} (nemesis/partition-random-halves)
            {
            :node-start :start
            :node-stop :stop} (nemesis/partition-random-node)
            {
            :hammer-start :start
            :hammer-stop :stop} (nemesis/hammer-time "dotnet")
          })
          ;; :nemesis (nemesis/hammer-time "dotnet")
          :generator (->> (ela/gen {:key-count 10, :min-txn-length 1, :max-txn-length 5, :max-writes-per-key 32}) ;; max-writes-per-key (32 default)
                          ;; (gen/mix [r w ela/gen])
                          (gen/stagger 1/10)
                          (gen/nemesis 
                            nil)
                          (gen/time-limit 180))
          }))

(defn -main
  "Handles command line arguments. Can either run a test, or a web server for
  browsing results."
  [& args]
  (cli/run! (merge (cli/single-test-cmd {:test-fn etcd-test})
                   (cli/serve-cmd))
            args))

;; (defn -main
;;   "Handles command line arguments. Can either run a test, or a web server for
;;   browsing results."
;;   [& args]
;;   (print (:body (httpclient/post "https://localhost:5001/" {:insecure? true :form-params {:operations "[[:r 1 nil] [:append 1 5] [:r 1 nil]]"}
;;                                                                                             :content-type :json
;;                                                                                             }))))